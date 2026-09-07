import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/login']);
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (!auth.isAuthenticated()) return inject(Router).createUrlTree(['/login']);
  return auth.isAdmin() ? true : inject(Router).createUrlTree(['/requests']);
};

export const staffGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (!auth.isAuthenticated()) return inject(Router).createUrlTree(['/login']);
  return auth.isStaff() ? true : inject(Router).createUrlTree(['/requests']);
};

/** Already signed in? Skip the login page. */
export const anonymousGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).createUrlTree([auth.homeUrl()]) : true;
};
