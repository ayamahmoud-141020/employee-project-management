import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { Department } from '../../core/models/api.models';
import { DepartmentService } from '../../core/services/department.service';
import { NotificationService } from '../../core/services/notification.service';
import { ConfirmService } from '../../shared/confirm.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { DepartmentFormComponent, DepartmentFormData } from './department-form.component';

/**
 * The departments table.
 *
 * Not paged, unlike employees and projects — the endpoint returns every department because
 * forms elsewhere need the complete list, and there are tens of them rather than thousands.
 * Search is therefore a filter over one request rather than a new query per keystroke.
 */
@Component({
  selector: 'epm-department-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
    PageHeaderComponent,
  ],
  templateUrl: './department-list.component.html',
  styleUrl: './department-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DepartmentListComponent implements OnInit {
  private readonly departments = inject(DepartmentService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmService = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  readonly rows = signal<Department[]>([]);
  readonly loading = signal(false);

  readonly searchControl = new FormControl('', { nonNullable: true });

  readonly displayedColumns = ['name', 'description', 'employees', 'actions'];

  constructor() {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.load());
  }

  ngOnInit(): void {
    this.load();
  }

  create(): void {
    this.openForm({ mode: 'create' });
  }

  edit(department: Department): void {
    this.openForm({ mode: 'edit', department });
  }

  /**
   * Deletes a department, refusing early when it still has people in it.
   *
   * The API enforces this too and would answer 409 — checking here just means the user gets
   * told why instead of watching a confirm dialog turn into an error toast.
   */
  delete(department: Department): void {
    if (department.employeeCount > 0) {
      this.notifications.error(
        `${department.name} still has ${department.employeeCount} employee(s). ` +
          'Move them to another department before deleting it.',
      );

      return;
    }

    this.confirmService
      .confirm({
        title: 'Delete department?',
        message: `${department.name} will be permanently removed. This cannot be undone.`,
        confirmLabel: 'Delete',
        destructive: true,
      })
      .subscribe(() => {
        this.departments.delete(department.id).subscribe(() => {
          this.notifications.success(`${department.name} was deleted.`);
          this.load();
        });
      });
  }

  private load(): void {
    this.loading.set(true);

    this.departments.list(this.searchControl.value || undefined).subscribe({
      next: (departments) => {
        this.rows.set(departments);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private openForm(data: DepartmentFormData): void {
    this.dialog
      .open(DepartmentFormComponent, { data, width: '520px', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((saved?: Department) => {
        if (saved) {
          this.load();
        }
      });
  }
}
