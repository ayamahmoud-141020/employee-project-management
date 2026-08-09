import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay, tap } from 'rxjs';
import { ApiResponse, Department, SaveDepartmentRequest } from '../models/api.models';
import { handlesOwnErrors } from '../interceptors/error-context';

@Injectable({ providedIn: 'root' })
export class DepartmentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/departments';

  private cache?: Observable<Department[]>;

  /** Every department — the endpoint is not paged, because forms need the whole list. */
  list(search?: string): Observable<Department[]> {
    const url = search ? `${this.baseUrl}?search=${encodeURIComponent(search)}` : this.baseUrl;

    return this.http.get<ApiResponse<Department[]>>(url).pipe(map((response) => response.data!));
  }

  /**
   * The list as a dropdown source, fetched once and shared.
   *
   * Departments barely change and several forms need them, so re-requesting per dialog is
   * wasted traffic. `refCount: false` keeps the cached value after the last subscriber goes
   * away; every write below drops the cache so a renamed department cannot linger in a form.
   */
  forPicker(): Observable<Department[]> {
    this.cache ??= this.list().pipe(shareReplay({ bufferSize: 1, refCount: false }));

    return this.cache;
  }

  // The dialog reports failures on its own controls — see HANDLES_OWN_ERRORS.
  create(request: SaveDepartmentRequest): Observable<Department> {
    return this.http
      .post<ApiResponse<Department>>(this.baseUrl, request, { context: handlesOwnErrors() })
      .pipe(map((response) => response.data!), tap(() => this.invalidateCache()));
  }

  update(id: number, request: SaveDepartmentRequest): Observable<Department> {
    return this.http
      .put<ApiResponse<Department>>(`${this.baseUrl}/${id}`, request, { context: handlesOwnErrors() })
      .pipe(map((response) => response.data!), tap(() => this.invalidateCache()));
  }

  delete(id: number): Observable<void> {
    return this.http
      .delete<ApiResponse<void>>(`${this.baseUrl}/${id}`)
      .pipe(map(() => undefined), tap(() => this.invalidateCache()));
  }

  private invalidateCache(): void {
    this.cache = undefined;
  }
}
