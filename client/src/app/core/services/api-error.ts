import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';

/**
 * A failure from the API, unpacked from the response envelope.
 */
export interface ApiError {
  message: string;
  code?: string;
  /** Field-keyed validation messages, camelCased to match the form control names. */
  fieldErrors?: Record<string, string[]>;
  status: number;
}

/**
 * Pulls an ApiError out of whatever the HTTP layer produced.
 *
 * The API always answers in its own envelope, but a request can also fail before reaching it
 * — a dropped connection, a proxy 502 — and those have no envelope at all. Everything ends up
 * as the same shape here so callers never have to tell the two apart.
 */
export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { message: 'Something went wrong. Please try again.', status: 0 };
  }

  // status 0 means the request never got an answer: server down, DNS failure, CORS refusal.
  if (error.status === 0) {
    return {
      message: 'Cannot reach the server. Check your connection and try again.',
      status: 0,
    };
  }

  const body = error.error as
    | { message?: string; code?: string; errors?: Record<string, string[]> }
    | null
    | undefined;

  return {
    message: body?.message ?? defaultMessageFor(error.status),
    code: body?.code,
    fieldErrors: body?.errors,
    status: error.status,
  };
}

/**
 * Pushes server-side validation messages onto the matching form controls.
 *
 * The API camelCases its field keys to match the JSON it received, which is also what the
 * reactive form calls its controls — so the names line up without a translation table. Errors
 * for fields the form does not have are returned, so the caller can still surface them
 * somewhere rather than dropping them silently.
 */
export function applyFieldErrors(form: FormGroup, fieldErrors?: Record<string, string[]>): string[] {
  if (!fieldErrors) {
    return [];
  }

  const unmatched: string[] = [];

  for (const [field, messages] of Object.entries(fieldErrors)) {
    const control = form.get(field);

    if (control) {
      // `server` is a separate key from the built-in validators, so the message survives
      // until the user edits the field — at which point the form re-validates and clears it.
      control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }

  return unmatched;
}

function defaultMessageFor(status: number): string {
  switch (status) {
    case 401:
      return 'Your session has expired. Please sign in again.';
    case 403:
      return 'You do not have permission to do that.';
    case 404:
      return 'That item no longer exists.';
    case 409:
      return 'That change conflicts with the current data.';
    default:
      return 'Something went wrong. Please try again.';
  }
}
