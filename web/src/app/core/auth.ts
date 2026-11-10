import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, firstValueFrom, throwError } from 'rxjs';
import { problemMessage } from './problem';
import { ToastService } from './toast.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /** null until the session has been checked once. */
  readonly authenticated = signal<boolean | null>(null);

  async check(): Promise<boolean> {
    const known = this.authenticated();
    if (known !== null) {
      return known;
    }
    try {
      const session = await firstValueFrom(this.http.get<{ authenticated: boolean }>('/api/auth/session'));
      this.authenticated.set(session.authenticated);
      return session.authenticated;
    } catch {
      return false;
    }
  }

  async login(password: string): Promise<void> {
    await firstValueFrom(this.http.post('/api/auth/login', { password }));
    this.authenticated.set(true);
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } finally {
      this.authenticated.set(false);
      await this.router.navigateByUrl('/login');
    }
  }

  /** Called when any API call answers 401 (cookie expired or revoked). */
  sessionExpired(returnUrl: string): void {
    if (this.authenticated() === false) {
      return;
    }
    this.authenticated.set(false);
    void this.router.navigate(['/login'], { queryParams: returnUrl && returnUrl !== '/' ? { returnUrl } : {} });
  }
}

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  return (await auth.check()) || inject(Router).createUrlTree(['/login']);
};

export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  return !(await auth.check()) || inject(Router).createUrlTree(['/']);
};

/**
 * Adds the CSRF header the API requires on unsafe requests (decision D-35) and handles
 * failures every caller would handle the same way: expired sessions, network and server errors.
 * 4xx responses are left to the caller (forms map validation errors onto fields).
 */
export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }
  const auth = inject(AuthService);
  const router = inject(Router);
  const toasts = inject(ToastService);

  return next(request.clone({ setHeaders: { 'X-Requested-With': 'JaCrm' } })).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 401 && !request.url.startsWith('/api/auth/')) {
          auth.sessionExpired(router.url);
        } else if (error.status === 0 || error.status >= 500) {
          toasts.error(problemMessage(error, 'Something went wrong on the server.'));
        }
      }
      return throwError(() => error);
    }),
  );
};
