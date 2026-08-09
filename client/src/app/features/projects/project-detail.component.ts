import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Project, ProjectAssignment } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { ProjectService } from '../../core/services/project.service';
import { ConfirmService } from '../../shared/confirm.service';
import { statusBadgeClass } from '../../shared/status';
import { AssignEmployeeComponent, AssignEmployeeData } from './assign-employee.component';

/**
 * A single project and its team.
 *
 * The `id` arrives as an input rather than through ActivatedRoute — withComponentInputBinding
 * is enabled in app.config.ts, so a route param binds straight to a matching input.
 */
@Component({
  selector: 'epm-project-detail',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailComponent implements OnInit {
  private readonly projects = inject(ProjectService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmService = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  /** Route parameter, bound by the router. Arrives as a string, hence the Number(). */
  readonly id = input.required<string>();

  readonly canManage = this.auth.canManageAssignments;
  readonly badgeClass = statusBadgeClass;

  readonly project = signal<Project | null>(null);
  readonly team = signal<ProjectAssignment[]>([]);
  readonly loading = signal(false);
  readonly notFound = signal(false);

  readonly displayedColumns = ['employee', 'department', 'role', 'assignedDate', 'allocation', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  /** Sum of the team's allocations — a quick read on how heavily staffed the project is. */
  totalAllocation(): number {
    return this.team().reduce((total, member) => total + member.allocationPercentage, 0);
  }

  assign(): void {
    const project = this.project();

    if (!project) {
      return;
    }

    this.openAssignDialog({ mode: 'create', project });
  }

  editAssignment(assignment: ProjectAssignment): void {
    const project = this.project();

    if (!project) {
      return;
    }

    this.openAssignDialog({ mode: 'edit', project, assignment });
  }

  remove(assignment: ProjectAssignment): void {
    const projectId = Number(this.id());

    this.confirmService
      .confirm({
        title: 'Remove from project?',
        message: `${assignment.employeeName} will be taken off this project. Their employee record is unaffected.`,
        confirmLabel: 'Remove',
        destructive: true,
      })
      .subscribe(() => {
        this.projects.removeEmployee(projectId, assignment.employeeId).subscribe(() => {
          this.notifications.success(`${assignment.employeeName} was removed from the project.`);
          this.load();
        });
      });
  }

  private load(): void {
    const projectId = Number(this.id());
    this.loading.set(true);

    this.projects.getById(projectId).subscribe({
      next: (project) => {
        this.project.set(project);
        this.loadTeam(projectId);
      },
      error: () => {
        // A deleted or mistyped id should show an explanatory page, not an empty shell.
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  private loadTeam(projectId: number): void {
    this.projects.getTeam(projectId).subscribe({
      next: (team) => {
        this.team.set(team);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private openAssignDialog(data: AssignEmployeeData): void {
    this.dialog
      .open(AssignEmployeeComponent, { data, width: '520px', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((saved?: ProjectAssignment) => {
        if (saved) {
          this.load();
        }
      });
  }
}
