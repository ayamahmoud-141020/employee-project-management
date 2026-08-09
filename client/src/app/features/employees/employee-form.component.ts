import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Employee } from '../../core/models/api.models';
import { DepartmentService } from '../../core/services/department.service';
import { EmployeeService } from '../../core/services/employee.service';
import { NotificationService } from '../../core/services/notification.service';
import { applyFieldErrors, toApiError } from '../../core/services/api-error';
import { fromIsoDate, toIsoDate } from '../../shared/dates';

export type EmployeeFormData = { mode: 'create' } | { mode: 'edit'; employee: Employee };

/**
 * Create/edit dialog for an employee.
 *
 * The client-side rules mirror the API's, so the obvious mistakes are caught without a round
 * trip. They are not the enforcement — anything the server rejects comes back keyed by field
 * and is attached to the matching control by {@link applyFieldErrors}, which is how a
 * duplicate email ends up under the email box rather than in an anonymous toast.
 */
@Component({
  selector: 'epm-employee-form',
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
  templateUrl: './employee-form.component.html',
  styleUrl: './employee-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeFormComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly employees = inject(EmployeeService);
  private readonly departments = inject(DepartmentService);
  private readonly notifications = inject(NotificationService);

  readonly dialogRef = inject(MatDialogRef<EmployeeFormComponent, Employee | undefined>);
  readonly data = inject<EmployeeFormData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly generalError = signal<string | null>(null);
  readonly departmentOptions = toSignal(this.departments.forPicker(), { initialValue: [] });

  /** Blocks future dates in the picker itself, rather than only complaining after submit. */
  readonly today = new Date();

  readonly isEdit = this.data.mode === 'edit';

  readonly form = this.formBuilder.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phone: [''],
    jobTitle: ['', [Validators.required, Validators.maxLength(150)]],
    departmentId: [null as number | null, [Validators.required]],
    hireDate: [null as Date | null, [Validators.required]],
  });

  constructor() {
    if (this.data.mode === 'edit') {
      const employee = this.data.employee;

      this.form.patchValue({
        firstName: employee.firstName,
        lastName: employee.lastName,
        email: employee.email,
        phone: employee.phone ?? '',
        jobTitle: employee.jobTitle,
        departmentId: employee.departmentId,
        hireDate: fromIsoDate(employee.hireDate),
      });
    }
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();

      return;
    }

    this.saving.set(true);
    this.generalError.set(null);

    const value = this.form.getRawValue();

    const request = {
      firstName: value.firstName.trim(),
      lastName: value.lastName.trim(),
      email: value.email.trim(),
      phone: value.phone.trim() || null,
      jobTitle: value.jobTitle.trim(),
      departmentId: value.departmentId!,
      hireDate: toIsoDate(value.hireDate!),
    };

    const save$ =
      this.data.mode === 'edit'
        ? this.employees.update(this.data.employee.id, request)
        : this.employees.create(request);

    save$.subscribe({
      next: (employee) => {
        this.notifications.success(
          this.isEdit ? `${employee.fullName} was updated.` : `${employee.fullName} was added.`,
        );
        this.dialogRef.close(employee);
      },
      error: (error: unknown) => {
        this.saving.set(false);

        const apiError = toApiError(error);
        const unmatched = applyFieldErrors(this.form, apiError.fieldErrors);

        // Business conflicts (a duplicate email) arrive as a message with no field key, so
        // they would otherwise vanish. Pin them to the control they are really about.
        if (apiError.code === 'Employee.EmailExists') {
          this.form.controls.email.setErrors({ server: apiError.message });
          this.form.controls.email.markAsTouched();

          return;
        }

        this.generalError.set(
          unmatched.length > 0 ? unmatched.join(' ') : apiError.fieldErrors ? null : apiError.message,
        );
      },
    });
  }
}
