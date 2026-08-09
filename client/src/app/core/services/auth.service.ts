import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  ApiResponse,
  AuthenticatedUser,
  AuthenticationResponse,
  UserRole,
} from '../models/api.models';

const ACCESS_TOKEN_KEY = 'epm.accessToken';
const REFRESH_TOKEN_KEY = 'epm.refreshToken';
const USER_KEY = 'epm.user';

/**
 * Holds the signed-in session and answers "may this user do X?".
 *
 * Session state is a signal, so guards, the toolbar and every page react to a sign-in or
 * sign-out without any of them subscribing to anything.
 *
 * Tokens live in localStorage. That is a deliberate, documented trade: it survives a page
 * refresh and a new tab, which is what people expect, at the cost of being readable by any
 * script on the page. The stricter alternative is an httpOnly refresh cookie with the access
 * token kept in memory only — see the README's "Possible future improvements".
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly currentUser = signal<AuthenticatedUser | null>(readStoredUser());

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly role = computed(() => this.currentUser()?.role ?? null);

  /** True while a refresh is in flight, so the interceptor does not start a second one. */
  private refreshing = false;

  login(email: string, password: string): Observable<AuthenticatedUser> {
    return this.http
      .post<ApiResponse<AuthenticationResponse>>('/api/auth/login', { email, password })
      .pipe(
        map((response) => response.data!),
        tap((session) => this.storeSession(session)),
        map((session) => session.user),
      );
  }

  /**
   * Swaps the refresh token for a new access token.
   *
   * Returns the new access token so the interceptor can retry the request that triggered it.
   */
  refresh(): Observable<string> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);

    if (!refreshToken) {
      throw new Error('No refresh token available.');
    }

    this.refreshing = true;

    return this.http
      .post<ApiResponse<AuthenticationResponse>>('/api/auth/refresh', { refreshToken })
      .pipe(
        map((response) => response.data!),
        tap({
          next: (session) => {
            this.storeSession(session);
            this.refreshing = false;
          },
          error: () => {
            this.refreshing = false;
          },
        }),
        map((session) => session.accessToken),
      );
  }

  get isRefreshing(): boolean {
    return this.refreshing;
  }

  get accessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  logout(redirectTo: string = '/login'): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.currentUser.set(null);
    void this.router.navigate([redirectTo]);
  }

  /**
   * Whether the signed-in user holds one of the given roles.
   *
   * Used to hide controls the user cannot use. It is a convenience, not a security control —
   * the API enforces the same matrix, and a hidden button is still callable with curl.
   */
  hasRole(...roles: UserRole[]): boolean {
    const role = this.currentUser()?.role;

    return role != null && roles.includes(role);
  }

  // Named after capabilities rather than roles, matching the API's authorization policies —
  // when the role matrix changes, it changes here and in Policies.cs, not in every template.
  readonly canManageEmployees = computed(() => this.hasRole('Admin'));
  readonly canManageDepartments = computed(() => this.hasRole('Admin'));
  readonly canManageProjects = computed(() => this.hasRole('Admin', 'Manager'));
  readonly canManageAssignments = computed(() => this.hasRole('Admin', 'Manager'));

  private storeSession(session: AuthenticationResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, session.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, session.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(session.user));
    this.currentUser.set(session.user);
  }
}

function readStoredUser(): AuthenticatedUser | null {
  const raw = localStorage.getItem(USER_KEY);

  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as AuthenticatedUser;
  } catch {
    // Corrupted or hand-edited storage should log the user out, not crash the app on boot.
    localStorage.removeItem(USER_KEY);

    return null;
  }
}
