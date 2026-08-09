import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  ApiResponse,
  AssignEmployeeRequest,
  MyAssignment,
  PagedResult,
  Project,
  ProjectAssignment,
  ProjectQuery,
  SaveProjectRequest,
} from '../models/api.models';
import { toHttpParams } from './http-params';
import { handlesOwnErrors } from '../interceptors/error-context';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/projects';

  list(query: ProjectQuery): Observable<PagedResult<Project>> {
    return this.http
      .get<ApiResponse<PagedResult<Project>>>(this.baseUrl, { params: toHttpParams(query) })
      .pipe(map((response) => response.data!));
  }

  getById(id: number): Observable<Project> {
    return this.http
      .get<ApiResponse<Project>>(`${this.baseUrl}/${id}`)
      .pipe(map((response) => response.data!));
  }

  // Form-backed writes report failures on their own controls — see HANDLES_OWN_ERRORS.
  create(request: SaveProjectRequest): Observable<Project> {
    return this.http
      .post<ApiResponse<Project>>(this.baseUrl, request, { context: handlesOwnErrors() })
      .pipe(map((response) => response.data!));
  }

  update(id: number, request: SaveProjectRequest): Observable<Project> {
    return this.http
      .put<ApiResponse<Project>>(`${this.baseUrl}/${id}`, request, { context: handlesOwnErrors() })
      .pipe(map((response) => response.data!));
  }

  /** Permanent, unlike employee deletion — its assignments go with it. */
  delete(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${id}`).pipe(map(() => undefined));
  }

  getTeam(projectId: number): Observable<ProjectAssignment[]> {
    return this.http
      .get<ApiResponse<ProjectAssignment[]>>(`${this.baseUrl}/${projectId}/employees`)
      .pipe(map((response) => response.data!));
  }

  assignEmployee(projectId: number, request: AssignEmployeeRequest): Observable<ProjectAssignment> {
    return this.http
      .post<ApiResponse<ProjectAssignment>>(`${this.baseUrl}/${projectId}/employees`, request, {
        context: handlesOwnErrors(),
      })
      .pipe(map((response) => response.data!));
  }

  updateAssignment(
    projectId: number,
    employeeId: number,
    request: { role: string; allocationPercentage: number },
  ): Observable<ProjectAssignment> {
    return this.http
      .put<ApiResponse<ProjectAssignment>>(
        `${this.baseUrl}/${projectId}/employees/${employeeId}`,
        request,
        { context: handlesOwnErrors() },
      )
      .pipe(map((response) => response.data!));
  }

  removeEmployee(projectId: number, employeeId: number): Observable<void> {
    return this.http
      .delete<ApiResponse<void>>(`${this.baseUrl}/${projectId}/employees/${employeeId}`)
      .pipe(map(() => undefined));
  }

  /**
   * The caller's own assignments.
   *
   * Takes no id — the server reads it from the token, which is what stops a User-role account
   * from reading anyone else's.
   */
  myAssignments(): Observable<MyAssignment[]> {
    return this.http
      .get<ApiResponse<MyAssignment[]>>('/api/me/assignments')
      .pipe(map((response) => response.data!));
  }
}
