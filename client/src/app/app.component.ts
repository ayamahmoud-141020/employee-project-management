import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Root component. Deliberately empty beyond the outlet — the chrome (toolbar, nav) belongs to
 * ShellComponent, which only wraps the authenticated routes. The login page has no chrome.
 */
@Component({
  selector: 'epm-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {}
