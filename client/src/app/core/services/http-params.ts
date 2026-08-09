import { HttpParams } from '@angular/common/http';

/**
 * Turns a query object into HttpParams, dropping anything not set.
 *
 * The omission matters: `?departmentId=` or `?isActive=null` is not the same request as
 * leaving the parameter off. The API treats a missing filter as "no filter" and an empty one
 * as a value to parse, so sending blanks would filter the list down to nothing.
 */
// Takes an object rather than Record<string, unknown> so callers can pass a declared
// interface: an interface without an index signature is not assignable to a Record, and
// widening every query type just to satisfy that would lose the field names entirely.
export function toHttpParams(query: object): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(query)) {
    if (value === null || value === undefined || value === '') {
      continue;
    }

    params = params.set(key, String(value));
  }

  return params;
}
