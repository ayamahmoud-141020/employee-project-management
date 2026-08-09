import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/api.models';

/**
 * Blocks a route when nobody is signed in, remembering where they were headed.
 *
 * The returnUrl is what makes a bookmarked deep link work: sign in, land back on the page you
 * actually asked for rather than the dashboard.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/**
 * Blocks a route the signed-in user's role does not cover.
 *
 * A convenience for the person using the app, not a security boundary — the API enforces the
 * same rules, and this only stops them reaching a page that would fail anyway.
 */
export function roleGuard(...roles: UserRole[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (auth.hasRole(...roles)) {
      return true;
    }

    // Sent to the dashboard rather than to /login: they are signed in, just not permitted,
    // and bouncing an authenticated user to a login form reads like a bug.
    return router.createUrlTree(['/dashboard']);
  };
}
