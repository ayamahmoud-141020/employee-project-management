import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { map } from 'rxjs/operators';
import type {
  AuthenticationResult,
  Configuration,
  PublicClientApplication,
} from '@azure/msal-browser';
import { ApiResponse, SsoConfiguration } from '../models/api.models';
import { AuthService } from './auth.service';

/**
 * Sign-in through Microsoft Entra ID.
 *
 * Three things make this worth a service of its own rather than code in the login component.
 * The settings arrive at runtime from `GET /api/auth/sso`, so the client is built once and
 * pointed at a tenant later. MSAL is imported dynamically, so a deployment with SSO switched
 * off never downloads it. And the redirect lands back on the page as a fresh document, so
 * "handle the response" and "start the request" are two separate entry points that have to
 * agree on one configured instance.
 *
 * MSAL is the one substantial dependency added for this. Authorization code with PKCE is a
 * protocol with real failure modes — nonce and state validation, token caching, silent renewal
 * through a hidden iframe — and Microsoft's own implementation of it is a better answer than
 * a hand-rolled one.
 */
@Injectable({ providedIn: 'root' })
export class SsoService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly configuration = signal<SsoConfiguration | null>(null);

  /** Null until the API has been asked; then the answer, enabled or not. */
  readonly ssoConfiguration = this.configuration.asReadonly();

  private client: PublicClientApplication | null = null;
  private clientPromise: Promise<PublicClientApplication> | null = null;
  private completedRedirect = false;

  /**
   * Loads the configuration, and — when SSO is on — completes any redirect in progress.
   *
   * Returns the user if this page load *was* the return leg of a sign-in, otherwise null.
   * Safe to call on every visit to the login page.
   */
  async initialize(): Promise<void> {
    const configuration = await this.loadConfiguration();

    if (!configuration.enabled) {
      return;
    }

    const client = await this.getClient(configuration);
    const redirectResult = await client.handleRedirectPromise();

    if (redirectResult) {
      await this.adoptToken(client, redirectResult);
    }
  }

  /** Starts the redirect to Entra ID. The browser leaves this page. */
  async signIn(): Promise<void> {
    const configuration = await this.loadConfiguration();

    if (!configuration.enabled) {
      throw new Error('Single sign-on is not enabled on this deployment.');
    }

    const client = await this.getClient(configuration);

    await client.loginRedirect({ scopes: scopesFor(configuration) });
  }

  /**
   * Ends the Entra ID session as well as this one.
   *
   * Clearing only the local session would leave the provider signed in, so the next press of
   * the SSO button would silently sign the same person straight back in — which is not what
   * anyone means by "sign out", least of all on a shared machine.
   */
  async signOut(): Promise<void> {
    const configuration = await this.loadConfiguration();

    if (!configuration.enabled) {
      return;
    }

    // Rebuilt rather than reusing `this.client`, which is null after a page refresh — the
    // session survives in storage but this service does not.
    const client = await this.getClient(configuration);
    const account = client.getActiveAccount() ?? client.getAllAccounts()[0];

    if (!account) {
      return;
    }

    await client.logoutRedirect({ account });
  }

  /**
   * True when *this* page load was the return leg of a sign-in.
   *
   * Deliberately not "an external session exists": someone who is already signed in and opens
   * the login page to switch accounts should see the form, not be bounced to the dashboard.
   */
  get hasCompletedRedirect(): boolean {
    return this.completedRedirect;
  }

  private async loadConfiguration(): Promise<SsoConfiguration> {
    const cached = this.configuration();

    if (cached) {
      return cached;
    }

    const loaded = await firstValueFrom(
      this.http
        .get<ApiResponse<SsoConfiguration>>('/api/auth/sso')
        .pipe(map((response) => response.data!)),
    );

    this.configuration.set(loaded);

    return loaded;
  }

  private async getClient(configuration: SsoConfiguration): Promise<PublicClientApplication> {
    // Memoised on the promise, not the resolved value: initialize() and a button click can
    // both reach this before the first one finishes, and MSAL must be constructed once.
    this.clientPromise ??= this.createClient(configuration);

    return this.clientPromise;
  }

  private async createClient(configuration: SsoConfiguration): Promise<PublicClientApplication> {
    const { PublicClientApplication: Msal } = await import('@azure/msal-browser');

    const client = new Msal(msalConfiguration(configuration));

    await client.initialize();

    this.client = client;

    // Registered here rather than at construction so AuthService never imports MSAL, which
    // would pull it into the main bundle and undo the dynamic import above.
    this.auth.registerExternalTokenRenewer(() => this.acquireTokenSilently());

    return client;
  }

  private async adoptToken(
    client: PublicClientApplication,
    result: AuthenticationResult,
  ): Promise<void> {
    client.setActiveAccount(result.account);

    await firstValueFrom(this.auth.signInWithExternalToken(result.accessToken));

    this.completedRedirect = true;
  }

  /**
   * A fresh access token without user interaction.
   *
   * Rejects when the provider's session has ended, which is the signal the interceptor needs
   * in order to sign the user out rather than retry forever.
   */
  private async acquireTokenSilently(): Promise<string> {
    const configuration = await this.loadConfiguration();
    const client = this.client ?? (await this.getClient(configuration));
    const account = client.getActiveAccount() ?? client.getAllAccounts()[0];

    if (!account) {
      throw new Error('No signed-in Entra ID account.');
    }

    const result = await client.acquireTokenSilent({
      account,
      scopes: scopesFor(configuration),
    });

    return result.accessToken;
  }
}

function msalConfiguration(configuration: SsoConfiguration): Configuration {
  return {
    auth: {
      clientId: configuration.clientId!,
      authority: configuration.authority!,
      // The sign-in page, so the return leg lands somewhere that knows how to finish it.
      redirectUri: `${window.location.origin}/login`,
      postLogoutRedirectUri: `${window.location.origin}/login`,
    },
    cache: {
      // Matches where this app already keeps its session, so a new tab and a page refresh
      // behave the same for both sign-in routes. Same trade as the access token — see the
      // note in AuthService.
      cacheLocation: 'localStorage',
    },
  };
}

function scopesFor(configuration: SsoConfiguration): string[] {
  // This API's scope only. An access token is issued for one resource, so adding a Graph
  // scope here would produce a token this API cannot accept. MSAL appends `openid`,
  // `profile` and `offline_access` itself — those are not resource scopes.
  return configuration.apiScope ? [configuration.apiScope] : [];
}
