import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Project, ProjectAssignment } from '../../core/models/api.models';
import { EmployeeService } from '../../core/services/employee.service';
import { NotificationService } from '../../core/services/notification.service';
import { ProjectService } from '../../core/services/project.service';
import { applyFieldErrors, toApiError } from '../../core/services/api-error';
import { fromIsoDate, toIsoDate } from '../../shared/dates';

export type AssignEmployeeData =
  | { mode: 'create'; project: Project }
  | { mode: 'edit'; project: Project; assignment: ProjectAssignment };

/**
 * Adds someone to a project team, or changes their role and allocation.
 *
 * The picker only offers active employees, and the date picker is bounded by the project's own
 * schedule — both rules the API would enforce anyway, applied here so the user cannot pick a
 * combination that is guaranteed to be rejected.
 *
 * On edit, the employee and date are locked: changing who an assignment is for is really a
 * remove-and-add, and moving the start date is a correction rather than routine editing.
 */
@Component({
  selector: 'epm-assign-employee',
  standalone: true,
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './assign-employee.component.html',
  styleUrl: './assign-employee.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssignEmployeeComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly projects = inject(ProjectService);
  private readonly employees = inject(EmployeeService);
  private readonly notifications = inject(NotificationService);

  readonly dialogRef = inject(MatDialogRef<AssignEmployeeComponent, ProjectAssignment | undefined>);
  readonly data = inject<AssignEmployeeData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly generalError = signal<string | null>(null);
  readonly isEdit = this.data.mode === 'edit';

  readonly employeeOptions = toSignal(this.employees.activeForPicker(), { initialValue: [] });

  readonly project = this.data.project;
  readonly minDate = fromIsoDate(this.project.startDate);
  readonly maxDate = this.project.endDate ? fromIsoDate(this.project.endDate) : null;

  readonly form = this.formBuilder.nonNullable.group({
    employeeId: [null as number | null, [Validators.required]],
    role: ['', [Validators.required, Validators.maxLength(100)]],
    assignedDate: [null as Date | null, [Validators.required]],
    allocationPercentage: [50, [Validators.required, Validators.min(1), Validators.max(100)]],
  });

  readonly scheduleHint = computed(() =>
    this.maxDate
      ? `Must fall within the project schedule.`
      : `Cannot be earlier than the project start date.`,
  );

  constructor() {
    if (this.data.mode === 'edit') {
      const assignment = this.data.assignment;

      this.form.patchValue({
        employeeId: assignment.employeeId,
        role: assignment.role,
        assignedDate: fromIsoDate(assignment.assignedDate),
        allocationPercentage: assignment.allocationPercentage,
      });

      this.form.controls.employeeId.disable();
      this.form.controls.assignedDate.disable();
    } else {
      // Default to the project's start date when it is in the past, otherwise today — either
      // way a date that already satisfies the schedule rule.
      const today = new Date();
      this.form.controls.assignedDate.setValue(this.minDate > today ? this.minDate : today);
    }
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();

      return;
    }

    this.saving.set(true);
    this.generalError.set(null);

    // getRawValue includes the disabled controls, which `value` would omit.
    const value = this.form.getRawValue();

    const save$ =
      this.data.mode === 'edit'
        ? this.projects.updateAssignment(this.project.id, value.employeeId!, {
            role: value.role.trim(),
            allocationPercentage: value.allocationPercentage,
          })
        : this.projects.assignEmployee(this.project.id, {
            employeeId: value.employeeId!,
            role: value.role.trim(),
            assignedDate: toIsoDate(value.assignedDate!),
            allocationPercentage: value.allocationPercentage,
          });

    save$.subscribe({
      next: (assignment) => {
        this.notifications.success(
          this.isEdit
            ? `${assignment.employeeName}'s assignment was updated.`
            : `${assignment.employeeName} was added to ${this.project.name}.`,
        );
        this.dialogRef.close(assignment);
      },
      error: (error: unknown) => {
        this.saving.set(false);

        const apiError = toApiError(error);
        const unmatched = applyFieldErrors(this.form, apiError.fieldErrors);

        // Business rules from the aggregate arrive as a code with no field. Pin each to the
        // control the user has to change to fix it.
        switch (apiError.code) {
          case 'Assignment.Duplicate':
          case 'Assignment.EmployeeInactive':
            this.form.controls.employeeId.setErrors({ server: apiError.message });
            this.form.controls.employeeId.markAsTouched();

            return;

          case 'Assignment.DateOutsideProjectSchedule':
            this.form.controls.assignedDate.setErrors({ server: apiError.message });
            this.form.controls.assignedDate.markAsTouched();

            return;

          default:
            this.generalError.set(
              unmatched.length > 0
                ? unmatched.join(' ')
                : apiError.fieldErrors
                  ? null
                  : apiError.message,
            );
        }
      },
    });
  }
}
