import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  ApiResponse,
  Employee,
  EmployeeDetail,
  EmployeeQuery,
  PagedResult,
  SaveEmployeeRequest,
} from '../models/api.models';
import { toHttpParams } from './http-params';
import { handlesOwnErrors } from '../interceptors/error-context';

@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/employees';

  /**
   * One page of employees. Search, filter, sort and paging are all decided by the server —
   * the component never holds more than the rows currently on screen.
   */
  list(query: EmployeeQuery): Observable<PagedResult<Employee>> {
    return this.http
      .get<ApiResponse<PagedResult<Employee>>>(this.baseUrl, { params: toHttpParams(query) })
      .pipe(map((response) => response.data!));
  }

  getById(id: number): Observable<EmployeeDetail> {
    return this.http
      .get<ApiResponse<EmployeeDetail>>(`${this.baseUrl}/${id}`)
      .pipe(map((response) => response.data!));
  }

  // The dialog maps failures onto its own controls, so these opt out of the global toast —
  // see HANDLES_OWN_ERRORS.
  create(request: SaveEmployeeRequest): Observable<Employee> {
    return this.http
      .post<ApiResponse<Employee>>(this.baseUrl, request, { context: handlesOwnErrors() })
      .pipe(map((response) => response.data!));
  }

  update(id: number, request: SaveEmployeeRequest): Observable<Employee> {
    return this.http
      .put<ApiResponse<Employee>>(`${this.baseUrl}/${id}`, request, { context: handlesOwnErrors() })
      .pipe(map((response) => response.data!));
  }

  /** DELETE deactivates rather than removing — the record and its history are kept. */
  deactivate(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${id}`).pipe(map(() => undefined));
  }

  reactivate(id: number): Observable<void> {
    return this.http
      .post<ApiResponse<void>>(`${this.baseUrl}/${id}/reactivate`, {})
      .pipe(map(() => undefined));
  }

  /**
   * Active employees only, for the "assign someone to this project" picker.
   *
   * Inactive people are excluded because the API would refuse the assignment anyway — better
   * to leave them out of the list than to offer a choice that always fails.
   */
  activeForPicker(search?: string): Observable<Employee[]> {
    const params = new HttpParams()
      .set('pageSize', '100')
      .set('isActive', 'true')
      .set('sortBy', 'lastName')
      .set('search', search ?? '');

    return this.http
      .get<ApiResponse<PagedResult<Employee>>>(this.baseUrl, { params })
      .pipe(map((response) => response.data!.items));
  }
}
