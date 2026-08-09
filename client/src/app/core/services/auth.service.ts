import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, from, tap, throwError } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  ApiResponse,
  AuthenticatedUser,
  AuthenticationResponse,
  CurrentUser,
  UserRole,
} from '../models/api.models';

const ACCESS_TOKEN_KEY = 'epm.accessToken';
const REFRESH_TOKEN_KEY = 'epm.refreshToken';
const USER_KEY = 'epm.user';
const SESSION_KIND_KEY = 'epm.sessionKind';

/**
 * How the current session was established.
 *
 * The two renew their access tokens by different means — a local session posts its refresh
 * token to this API, an external one asks the identity provider — so the session has to
 * remember which it is.
 */
export type SessionKind = 'local' | 'external';

/** Asks the identity provider for a fresh access token. Registered by the SSO service. */
export type ExternalTokenRenewer = () => Promise<string>;

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

  private externalTokenRenewer: ExternalTokenRenewer | null = null;

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
   * Adopts an access token issued by the external identity provider.
   *
   * The token is the provider's, so this app cannot read a role out of it and be believed —
   * an SSO user's role is decided server-side during claims transformation. `auth/me` is
   * therefore the authority on who just signed in, and calling it doubles as proof the API
   * accepts the token before the user is sent to a page that assumes it does.
   */
  signInWithExternalToken(accessToken: string): Observable<AuthenticatedUser> {
    localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(SESSION_KIND_KEY, 'external');

    return this.http.get<ApiResponse<CurrentUser>>('/api/auth/me').pipe(
      map((response) => toAuthenticatedUser(response.data!)),
      tap({
        next: (user) => {
          localStorage.setItem(USER_KEY, JSON.stringify(user));
          this.currentUser.set(user);
        },
        // A token the API will not accept must not leave a half-signed-in session behind.
        error: () => this.clearSession(),
      }),
    );
  }

  /** Lets the SSO service supply token renewal without this service importing MSAL. */
  registerExternalTokenRenewer(renew: ExternalTokenRenewer): void {
    this.externalTokenRenewer = renew;
  }

  get sessionKind(): SessionKind {
    return localStorage.getItem(SESSION_KIND_KEY) === 'external' ? 'external' : 'local';
  }

  /**
   * Obtains a fresh access token, by whichever route this session was established.
   *
   * Returns the new access token so the interceptor can retry the request that triggered it.
   */
  refresh(): Observable<string> {
    return this.sessionKind === 'external' ? this.renewExternalToken() : this.renewLocalToken();
  }

  get isRefreshing(): boolean {
    return this.refreshing;
  }

  get accessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  logout(redirectTo: string = '/login'): void {
    this.clearSession();
    void this.router.navigate([redirectTo]);
  }

  private renewLocalToken(): Observable<string> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);

    if (!refreshToken) {
      // An error notification rather than a synchronous throw: the interceptor's catchError
      // is what signs the user out, and it only runs on the returned stream.
      return throwError(() => new Error('No refresh token available.'));
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

  /**
   * Renews through the identity provider, which succeeds silently while the provider's own
   * session is alive and fails once it is not — at which point the interceptor signs out.
   */
  private renewExternalToken(): Observable<string> {
    const renew = this.externalTokenRenewer;

    if (!renew) {
      return throwError(() => new Error('No external token renewer is registered.'));
    }

    this.refreshing = true;

    return from(renew()).pipe(
      tap({
        next: (token) => {
          localStorage.setItem(ACCESS_TOKEN_KEY, token);
          this.refreshing = false;
        },
        error: () => {
          this.refreshing = false;
        },
      }),
    );
  }

  private clearSession(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(SESSION_KIND_KEY);
    this.currentUser.set(null);
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

function toAuthenticatedUser(user: CurrentUser): AuthenticatedUser {
  return {
    id: user.id,
    email: user.email,
    displayName: user.displayName,
    role: user.role,
    employeeId: user.employeeId,
  };
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
