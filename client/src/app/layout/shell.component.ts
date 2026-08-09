import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/services/auth.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  /** Rendered only when the signed-in user's role is in this list. */
  visibleTo?: readonly ('Admin' | 'Manager' | 'User')[];
}

/**
 * Chrome for the authenticated part of the app: toolbar, navigation, account menu.
 */
@Component({
  selector: 'epm-shell',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {
  private readonly auth = inject(AuthService);

  readonly user = this.auth.user;
  readonly menuOpen = signal(false);

  private readonly allItems: readonly NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
    { label: 'Employees', icon: 'groups', route: '/employees' },
    { label: 'Projects', icon: 'work_outline', route: '/projects' },
    { label: 'Departments', icon: 'apartment', route: '/departments', visibleTo: ['Admin'] },
    { label: 'My assignments', icon: 'assignment_ind', route: '/my-assignments' },
  ];

  /**
   * Nav items this user can actually reach. Links to pages the role guard would bounce them
   * off are hidden rather than shown and then rejected.
   */
  readonly navItems = computed(() => {
    const role = this.user()?.role;

    return this.allItems.filter((item) => !item.visibleTo || (role != null && item.visibleTo.includes(role)));
  });

  readonly initials = computed(() => {
    const name = this.user()?.displayName ?? '';

    return name
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  logout(): void {
    this.auth.logout();
  }
}
