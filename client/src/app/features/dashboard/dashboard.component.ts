import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Dashboard } from '../../core/models/api.models';
import { DashboardService } from '../../core/services/dashboard.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { statusBadgeClass } from '../../shared/status';

@Component({
  selector: 'epm-dashboard',
  standalone: true,
  imports: [RouterLink, MatIconModule, MatProgressBarModule, PageHeaderComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  private readonly dashboard = inject(DashboardService);

  readonly data = signal<Dashboard | null>(null);
  readonly loading = signal(true);
  readonly badgeClass = statusBadgeClass;

  /**
   * The largest headcount in any one department, used to scale the bars.
   *
   * Scaling to the largest value rather than to the total is what makes small differences
   * visible — with five departments, percentages of the whole would all render as short stubs.
   * Floored at 1 so an empty database cannot divide by zero.
   */
  readonly maxHeadcount = computed(() =>
    Math.max(1, ...(this.data()?.employeesByDepartment.map((d) => d.employeeCount) ?? [1])),
  );

  readonly maxStatusCount = computed(() =>
    Math.max(1, ...(this.data()?.projectsByStatus.map((s) => s.count) ?? [1])),
  );

  ngOnInit(): void {
    this.dashboard.load().subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  barWidth(value: number, max: number): number {
    return Math.round((value / max) * 100);
  }
}
