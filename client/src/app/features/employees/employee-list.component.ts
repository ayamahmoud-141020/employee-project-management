import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { Employee } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { DepartmentService } from '../../core/services/department.service';
import { EmployeeService } from '../../core/services/employee.service';
import { NotificationService } from '../../core/services/notification.service';
import { ConfirmService } from '../../shared/confirm.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { EmployeeFormComponent, EmployeeFormData } from './employee-form.component';

/**
 * The employees table.
 *
 * Search, filtering, sorting and paging are all decided by the server — this component holds
 * one page of rows and a description of what it asked for, never the full table. Every control
 * therefore ends in the same place: adjust the criteria, reset to page 1 where that matters,
 * and reload.
 */
@Component({
  selector: 'epm-employee-list',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
    PageHeaderComponent,
  ],
  templateUrl: './employee-list.component.html',
  styleUrl: './employee-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeListComponent implements OnInit {
  private readonly employees = inject(EmployeeService);
  private readonly departments = inject(DepartmentService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmService = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  readonly canManage = this.auth.canManageEmployees;

  readonly rows = signal<Employee[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);

  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly sortBy = signal('lastName');
  readonly sortDescending = signal(false);
  readonly departmentFilter = signal<number | null>(null);
  readonly activeFilter = signal<boolean | null>(null);

  readonly searchControl = new FormControl('', { nonNullable: true });

  readonly departmentOptions = toSignal(this.departments.forPicker(), { initialValue: [] });

  /** The action column only exists for roles that can act on a row. */
  readonly displayedColumns = computed(() => {
    const columns = ['name', 'email', 'jobTitle', 'department', 'hireDate', 'status'];

    return this.canManage() ? [...columns, 'actions'] : columns;
  });

  readonly hasFilters = computed(
    () =>
      this.searchControl.value.length > 0 ||
      this.departmentFilter() !== null ||
      this.activeFilter() !== null,
  );

  constructor() {
    // Debounced so typing does not fire a request per keystroke; distinctUntilChanged then
    // drops the ones where the text ended up unchanged (arrow keys, type-then-delete).
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        // A new search on page 4 usually lands on an empty page, which reads as "no results"
        // for a search that actually matched. Always restart at the first page.
        this.pageIndex.set(0);
        this.load();
      });
  }

  ngOnInit(): void {
    this.load();
  }

  onSortChange(sort: Sort): void {
    // Material yields an empty direction when the user cycles sorting off. Falling back to the
    // default keeps the order deterministic — an unsorted paged query can repeat or skip rows.
    this.sortBy.set(sort.direction ? sort.active : 'lastName');
    this.sortDescending.set(sort.direction === 'desc');
    this.pageIndex.set(0);
    this.load();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onDepartmentChange(departmentId: number | null): void {
    this.departmentFilter.set(departmentId);
    this.pageIndex.set(0);
    this.load();
  }

  onActiveChange(isActive: boolean | null): void {
    this.activeFilter.set(isActive);
    this.pageIndex.set(0);
    this.load();
  }

  clearFilters(): void {
    // emitEvent: false so this does not also trip the debounced valueChanges reload above and
    // fire a second, identical request.
    this.searchControl.setValue('', { emitEvent: false });
    this.departmentFilter.set(null);
    this.activeFilter.set(null);
    this.pageIndex.set(0);
    this.load();
  }

  create(): void {
    this.openForm({ mode: 'create' });
  }

  edit(employee: Employee): void {
    this.openForm({ mode: 'edit', employee });
  }

  deactivate(employee: Employee): void {
    this.confirmService
      .confirm({
        title: 'Deactivate employee?',
        message:
          `${employee.fullName} will be marked inactive and removed from any project they are ` +
          'currently assigned to. The record and its history are kept, and they can be reactivated later.',
        confirmLabel: 'Deactivate',
        destructive: true,
      })
      .subscribe(() => {
        this.employees.deactivate(employee.id).subscribe(() => {
          this.notifications.success(`${employee.fullName} was deactivated.`);
          this.load();
        });
      });
  }

  reactivate(employee: Employee): void {
    this.employees.reactivate(employee.id).subscribe(() => {
      this.notifications.success(`${employee.fullName} was reactivated.`);
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);

    this.employees
      .list({
        page: this.pageIndex() + 1, // The API pages from 1; MatPaginator counts from 0.
        pageSize: this.pageSize(),
        search: this.searchControl.value || null,
        sortBy: this.sortBy(),
        sortDescending: this.sortDescending(),
        departmentId: this.departmentFilter(),
        isActive: this.activeFilter(),
      })
      .subscribe({
        next: (page) => {
          this.rows.set(page.items);
          this.totalCount.set(page.totalCount);
          this.loading.set(false);
        },
        // The error interceptor has already told the user; this just clears the spinner so the
        // table does not sit loading forever.
        error: () => this.loading.set(false),
      });
  }

  private openForm(data: EmployeeFormData): void {
    this.dialog
      .open(EmployeeFormComponent, { data, width: '560px', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((saved?: Employee) => {
        if (saved) {
          this.load();
        }
      });
  }
}
