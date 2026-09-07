import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Adds the bearer token to API calls and, on a 401, refreshes once and retries. The sign-in
 * endpoints are left alone so a bad password is a plain 401 rather than a refresh attempt.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const isApi = req.url.startsWith('/api/');
  const isAuthCall = req.url.startsWith('/api/auth/');

  if (!isApi || isAuthCall) {
    return next(req);
  }

  return next(withToken(req, auth.accessToken)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !auth.refreshToken) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap((result) => next(withToken(req, result.accessToken))),
        catchError((refreshError: unknown) => {
          auth.logout();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};

function withToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}
