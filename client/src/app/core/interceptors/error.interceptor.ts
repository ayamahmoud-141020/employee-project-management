import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../services/notification.service';
import { toApiError } from '../services/api-error';
import { HANDLES_OWN_ERRORS } from './error-context';

/**
 * Surfaces failures the calling component has no better answer for.
 *
 * Three categories are deliberately left alone:
 *
 *  - 400 validation errors, which belong on the individual form controls rather than in a
 *    toast floating over the page. The form component maps those itself.
 *  - 401, which the auth interceptor is already handling by refreshing or signing out. A
 *    toast here would fire on every recovered request.
 *  - anything the caller marked with {@link HANDLES_OWN_ERRORS} — a business conflict from a
 *    form, which the form pins to the control it is about. Without the opt-out a duplicate
 *    email would be reported twice: once under the field and once in a toast.
 *
 * The error is always re-thrown, so a component that wants to react to a specific failure
 * still can — this only guarantees nothing fails silently.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((error: unknown) => {
      const apiError = toApiError(error);

      const handledElsewhere =
        apiError.status === 401 ||
        apiError.fieldErrors !== undefined ||
        request.context.get(HANDLES_OWN_ERRORS);

      if (!handledElsewhere) {
        notifications.error(apiError.message);
      }

      return throwError(() => error);
    }),
  );
};
