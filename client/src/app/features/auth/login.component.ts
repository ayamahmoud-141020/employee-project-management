import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SsoService } from '../../core/services/sso.service';
import { toApiError } from '../../core/services/api-error';

@Component({
  selector: 'epm-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly sso = inject(SsoService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showPassword = signal(false);

  /** True from the moment the SSO button is pressed until the browser leaves the page. */
  readonly redirecting = signal(false);

  readonly ssoConfiguration = this.sso.ssoConfiguration;

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  /**
   * Asks the API whether SSO is on, and finishes a sign-in if this load is the return leg of
   * one. Both happen here because the redirect comes back to this route as a new document —
   * the component that started the sign-in no longer exists.
   */
  async ngOnInit(): Promise<void> {
    try {
      await this.sso.initialize();
    } catch (error: unknown) {
      this.errorMessage.set(toApiError(error).message);

      return;
    }

    if (this.sso.hasCompletedRedirect) {
      void this.router.navigateByUrl(this.returnUrl());
    }
  }

  async signInWithMicrosoft(): Promise<void> {
    this.redirecting.set(true);
    this.errorMessage.set(null);

    try {
      await this.sso.signIn();
    } catch (error: unknown) {
      // Reached only if the redirect never starts; on success the browser has already left.
      this.redirecting.set(false);
      this.errorMessage.set(toApiError(error).message);
    }
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      // Touching everything makes the messages appear for fields the user never focused,
      // which is the usual way someone gets stuck on a form that "won't submit".
      this.form.markAllAsTouched();

      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: () => {
        void this.router.navigateByUrl(this.returnUrl());
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        // Shown inline rather than as a toast: the message belongs next to the form it is
        // about, and a failed sign-in is not a background event.
        this.errorMessage.set(toApiError(error).message);
      },
    });
  }

  togglePassword(): void {
    this.showPassword.update((visible) => !visible);
  }

  /** Back to wherever the guard interrupted them, or the dashboard on a direct visit. */
  private returnUrl(): string {
    return this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
  }
}
