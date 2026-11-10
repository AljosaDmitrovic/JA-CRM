import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth';
import { problemMessage } from '../../core/problem';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-login',
  imports: [FormsModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="login">
      <form (ngSubmit)="submit()" class="card" aria-labelledby="login-title">
        <span class="logo" aria-hidden="true">J</span>
        <h1 id="login-title">Job Search</h1>
        <p class="muted">Sign in to continue.</p>

        <div class="field">
          <label for="password">Password</label>
          <input
            id="password"
            name="password"
            class="input"
            type="password"
            autocomplete="current-password"
            required
            autofocus
            [class.invalid]="error()"
            [attr.aria-invalid]="!!error()"
            aria-describedby="login-error"
            [ngModel]="password()"
            (ngModelChange)="password.set($event)"
          />
        </div>

        <p id="login-error" class="error" role="alert">{{ error() }}</p>

        <button type="submit" class="btn btn-primary" [disabled]="busy() || !password()">
          @if (busy()) {
            <app-icon name="loader" />
          }
          Sign in
        </button>
      </form>
    </main>
  `,
  styles: `
    .login {
      display: grid;
      place-items: center;
      min-height: 100vh;
      min-height: 100dvh;
      padding: var(--s-4);
      background: var(--bg-subtle);
    }
    .card {
      display: flex;
      flex-direction: column;
      gap: var(--s-3);
      width: 100%;
      max-width: 340px;
      padding: var(--s-8);
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      background: var(--surface);
    }
    .logo {
      display: grid;
      place-items: center;
      width: 32px;
      height: 32px;
      margin-bottom: var(--s-2);
      border-radius: 7px;
      background: var(--text);
      color: var(--bg);
      font-weight: 700;
    }
    h1 {
      font-size: var(--text-lg);
    }
    .error {
      min-height: 1.2em;
      color: var(--danger);
      font-size: var(--text-sm);
    }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly password = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected async submit(): Promise<void> {
    if (!this.password() || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.login(this.password());
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      await this.router.navigateByUrl(returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/');
    } catch (error) {
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 429
          ? 'Too many attempts. Wait a minute and try again.'
          : problemMessage(error, 'Sign in failed.'),
      );
      this.password.set('');
    } finally {
      this.busy.set(false);
    }
  }
}
