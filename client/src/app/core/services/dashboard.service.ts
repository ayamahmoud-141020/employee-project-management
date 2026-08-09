import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiResponse, Dashboard } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  load(): Observable<Dashboard> {
    return this.http
      .get<ApiResponse<Dashboard>>('/api/dashboard')
      .pipe(map((response) => response.data!));
  }
}
