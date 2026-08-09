import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { PROJECT_STATUSES, Project, ProjectStatus } from '../../core/models/api.models';
import { NotificationService } from '../../core/services/notification.service';
import { ProjectService } from '../../core/services/project.service';
import { applyFieldErrors, toApiError } from '../../core/services/api-error';
import { fromIsoDate, toIsoDate } from '../../shared/dates';

export type ProjectFormData = { mode: 'create' } | { mode: 'edit'; project: Project };

/**
 * Validates that the end date is not before the start date.
 *
 * A group-level validator because the rule is about the relationship between two controls —
 * neither field is wrong on its own. The message is attached to endDate in the template,
 * because that is the one the user can most usefully change.
 */
function endDateNotBeforeStart(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value as Date | null;
  const end = group.get('endDate')?.value as Date | null;

  if (!start || !end) {
    return null;
  }

  return end < start ? { endBeforeStart: true } : null;
}

@Component({
  selector: 'epm-project-form',
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
  templateUrl: './project-form.component.html',
  styleUrl: './project-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectFormComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly projects = inject(ProjectService);
  private readonly notifications = inject(NotificationService);

  readonly dialogRef = inject(MatDialogRef<ProjectFormComponent, Project | undefined>);
  readonly data = inject<ProjectFormData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly generalError = signal<string | null>(null);
  readonly statuses = PROJECT_STATUSES;
  readonly isEdit = this.data.mode === 'edit';

  readonly form = this.formBuilder.nonNullable.group(
    {
      name: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.maxLength(2000)]],
      startDate: [null as Date | null, [Validators.required]],
      endDate: [null as Date | null],
      status: ['Planning' as ProjectStatus, [Validators.required]],
    },
    { validators: endDateNotBeforeStart },
  );

  constructor() {
    if (this.data.mode === 'edit') {
      const project = this.data.project;

      this.form.patchValue({
        name: project.name,
        description: project.description ?? '',
        startDate: fromIsoDate(project.startDate),
        endDate: project.endDate ? fromIsoDate(project.endDate) : null,
        status: project.status,
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
      name: value.name.trim(),
      description: value.description.trim() || null,
      startDate: toIsoDate(value.startDate!),
      // Null means open-ended, which the API supports — not a missing value.
      endDate: value.endDate ? toIsoDate(value.endDate) : null,
      status: value.status,
    };

    const save$ =
      this.data.mode === 'edit'
        ? this.projects.update(this.data.project.id, request)
        : this.projects.create(request);

    save$.subscribe({
      next: (project) => {
        this.notifications.success(
          this.isEdit ? `${project.name} was updated.` : `${project.name} was created.`,
        );
        this.dialogRef.close(project);
      },
      error: (error: unknown) => {
        this.saving.set(false);

        const apiError = toApiError(error);
        const unmatched = applyFieldErrors(this.form, apiError.fieldErrors);

        if (apiError.code === 'Project.NameExists') {
          this.form.controls.name.setErrors({ server: apiError.message });
          this.form.controls.name.markAsTouched();

          return;
        }

        // "The new schedule would strand N assignments" is about the dates but has no field
        // key, so it goes in the banner where it can be read in full.
        this.generalError.set(
          unmatched.length > 0 ? unmatched.join(' ') : apiError.fieldErrors ? null : apiError.message,
        );
      },
    });
  }
}
