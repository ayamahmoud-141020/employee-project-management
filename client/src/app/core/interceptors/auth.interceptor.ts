import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/** Endpoints that must not carry a token, and must never trigger a refresh. */
const ANONYMOUS_ROUTES = ['/api/auth/login', '/api/auth/refresh'];

/**
 * Queue for requests that arrive while a token refresh is already running.
 *
 * Without this, five widgets loading at once all get a 401, all start their own refresh, and
 * four of them present a token the first refresh has already rotated away — logging the user
 * out. Instead the first 401 refreshes and the rest wait here for the new token.
 */
const refreshedToken = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);

  if (isAnonymous(request)) {
    return next(request);
  }

  const token = auth.accessToken;
  const authorised = token ? withToken(request, token) : request;

  return next(authorised).pipe(
    catchError((error: unknown) => {
      const is401 = error instanceof HttpErrorResponse && error.status === 401;

      if (!is401 || !token) {
        return throwError(() => error);
      }

      if (auth.isRefreshing) {
        return refreshedToken.pipe(
          filter((value): value is string => value !== null),
          take(1),
          switchMap((fresh) => next(withToken(request, fresh))),
        );
      }

      // Cleared before the refresh starts, and the filter above is why. A BehaviorSubject
      // replays its current value to every new subscriber, so without this reset a request
      // arriving mid-refresh would be handed the *previous* token — the rotated-away one that
      // caused its 401 in the first place — and retry straight into another failure.
      refreshedToken.next(null);

      return auth.refresh().pipe(
        switchMap((fresh) => {
          refreshedToken.next(fresh);

          return next(withToken(request, fresh));
        }),
        catchError((refreshError: unknown) => {
          // The refresh token is expired, revoked or already used. Nothing left to try.
          auth.logout();

          return throwError(() => refreshError);
        }),
      );
    }),
  );
};

function isAnonymous(request: HttpRequest<unknown>): boolean {
  return ANONYMOUS_ROUTES.some((route) => request.url.includes(route));
}

function withToken(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
