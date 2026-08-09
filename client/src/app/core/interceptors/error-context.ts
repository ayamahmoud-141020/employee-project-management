import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * Marks a request whose failures the caller displays itself.
 *
 * Set on the create/update calls behind a form: those map the server's message onto the
 * offending control, so a toast saying the same thing is the error reported twice. The
 * interceptor cannot work this out for itself — a 409 from a form is handled inline, while a
 * 409 from a delete button has nowhere to go but a toast — so the caller says so explicitly.
 */
export const HANDLES_OWN_ERRORS = new HttpContextToken<boolean>(() => false);

/** Shorthand for the `context` option on an HttpClient call. */
export function handlesOwnErrors(): HttpContext {
  return new HttpContext().set(HANDLES_OWN_ERRORS, true);
}
