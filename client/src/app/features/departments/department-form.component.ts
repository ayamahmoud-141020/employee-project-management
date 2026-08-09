import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Department } from '../../core/models/api.models';
import { DepartmentService } from '../../core/services/department.service';
import { NotificationService } from '../../core/services/notification.service';
import { applyFieldErrors, toApiError } from '../../core/services/api-error';

export type DepartmentFormData = { mode: 'create' } | { mode: 'edit'; department: Department };

@Component({
  selector: 'epm-department-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './department-form.component.html',
  styleUrl: './department-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DepartmentFormComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly departments = inject(DepartmentService);
  private readonly notifications = inject(NotificationService);

  readonly dialogRef = inject(MatDialogRef<DepartmentFormComponent, Department | undefined>);
  readonly data = inject<DepartmentFormData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly generalError = signal<string | null>(null);
  readonly isEdit = this.data.mode === 'edit';

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
  });

  constructor() {
    if (this.data.mode === 'edit') {
      this.form.patchValue({
        name: this.data.department.name,
        description: this.data.department.description ?? '',
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
      // Blank means "no description"; sending an empty string would store one.
      description: value.description.trim() || null,
    };

    const save$ =
      this.data.mode === 'edit'
        ? this.departments.update(this.data.department.id, request)
        : this.departments.create(request);

    save$.subscribe({
      next: (department) => {
        this.notifications.success(
          this.isEdit ? `${department.name} was updated.` : `${department.name} was created.`,
        );
        this.dialogRef.close(department);
      },
      error: (error: unknown) => {
        this.saving.set(false);

        const apiError = toApiError(error);
        const unmatched = applyFieldErrors(this.form, apiError.fieldErrors);

        // A name clash comes back as a conflict with no field key, so pin it to the name box.
        if (apiError.code === 'Department.NameExists') {
          this.form.controls.name.setErrors({ server: apiError.message });
          this.form.controls.name.markAsTouched();

          return;
        }

        this.generalError.set(
          unmatched.length > 0 ? unmatched.join(' ') : apiError.fieldErrors ? null : apiError.message,
        );
      },
    });
  }
}
