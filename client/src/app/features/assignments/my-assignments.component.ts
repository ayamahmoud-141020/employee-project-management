import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { MyAssignment } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { ProjectService } from '../../core/services/project.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { statusBadgeClass } from '../../shared/status';

/**
 * The signed-in user's own project assignments.
 *
 * The request carries no employee id — the server reads it from the token — so there is
 * nothing here a User-role account could change to see somebody else's work.
 */
@Component({
  selector: 'epm-my-assignments',
  standalone: true,
  imports: [DatePipe, RouterLink, MatIconModule, MatProgressBarModule, PageHeaderComponent],
  templateUrl: './my-assignments.component.html',
  styleUrl: './my-assignments.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyAssignmentsComponent implements OnInit {
  private readonly projects = inject(ProjectService);
  private readonly auth = inject(AuthService);

  readonly assignments = signal<MyAssignment[]>([]);
  readonly loading = signal(true);
  readonly badgeClass = statusBadgeClass;

  /**
   * An account with no linked employee record — a service admin, say — has no assignments by
   * definition, and saying so beats an unexplained empty list.
   */
  readonly hasEmployeeRecord = () => this.auth.user()?.employeeId != null;

  ngOnInit(): void {
    this.projects.myAssignments().subscribe({
      next: (assignments) => {
        this.assignments.set(assignments);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  totalAllocation(): number {
    return this.assignments().reduce((total, a) => total + a.allocationPercentage, 0);
  }
}
