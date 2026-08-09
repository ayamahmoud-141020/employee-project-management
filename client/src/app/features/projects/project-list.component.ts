import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { PROJECT_STATUSES, Project, ProjectStatus } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { ProjectService } from '../../core/services/project.service';
import { ConfirmService } from '../../shared/confirm.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { statusBadgeClass } from '../../shared/status';
import { ProjectFormComponent, ProjectFormData } from './project-form.component';

@Component({
  selector: 'epm-project-list',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
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
    PageHeaderComponent,
  ],
  templateUrl: './project-list.component.html',
  styleUrl: './project-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectListComponent implements OnInit {
  private readonly projects = inject(ProjectService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmService = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  readonly canManage = this.auth.canManageProjects;
  readonly statuses = PROJECT_STATUSES;
  readonly badgeClass = statusBadgeClass;

  readonly rows = signal<Project[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);

  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly sortBy = signal('startDate');
  readonly sortDescending = signal(true);
  readonly statusFilter = signal<ProjectStatus | null>(null);

  readonly searchControl = new FormControl('', { nonNullable: true });

  readonly displayedColumns = computed(() => {
    const columns = ['name', 'status', 'startDate', 'endDate', 'team'];

    return this.canManage() ? [...columns, 'actions'] : columns;
  });

  readonly hasFilters = computed(
    () => this.searchControl.value.length > 0 || this.statusFilter() !== null,
  );

  constructor() {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.pageIndex.set(0);
        this.load();
      });
  }

  ngOnInit(): void {
    this.load();
  }

  onSortChange(sort: Sort): void {
    this.sortBy.set(sort.direction ? sort.active : 'startDate');
    this.sortDescending.set(sort.direction === 'desc');
    this.pageIndex.set(0);
    this.load();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  onStatusChange(status: ProjectStatus | null): void {
    this.statusFilter.set(status);
    this.pageIndex.set(0);
    this.load();
  }

  clearFilters(): void {
    this.searchControl.setValue('', { emitEvent: false });
    this.statusFilter.set(null);
    this.pageIndex.set(0);
    this.load();
  }

  create(): void {
    this.openForm({ mode: 'create' });
  }

  edit(project: Project): void {
    this.openForm({ mode: 'edit', project });
  }

  delete(project: Project): void {
    this.confirmService
      .confirm({
        title: 'Delete project?',
        message:
          `${project.name} and its ${project.assignedEmployeeCount} assignment(s) will be ` +
          'permanently removed. To keep the record instead, set its status to Cancelled.',
        confirmLabel: 'Delete',
        destructive: true,
      })
      .subscribe(() => {
        this.projects.delete(project.id).subscribe(() => {
          this.notifications.success(`${project.name} was deleted.`);
          this.load();
        });
      });
  }

  private load(): void {
    this.loading.set(true);

    this.projects
      .list({
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        search: this.searchControl.value || null,
        sortBy: this.sortBy(),
        sortDescending: this.sortDescending(),
        status: this.statusFilter(),
      })
      .subscribe({
        next: (page) => {
          this.rows.set(page.items);
          this.totalCount.set(page.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  private openForm(data: ProjectFormData): void {
    this.dialog
      .open(ProjectFormComponent, { data, width: '560px', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((saved?: Project) => {
        if (saved) {
          this.load();
        }
      });
  }
}
