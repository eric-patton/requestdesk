import { Routes } from '@angular/router';
import { adminGuard, anonymousGuard, authGuard } from './core/guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'requests' },
  {
    path: 'login',
    canActivate: [anonymousGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
    title: 'Sign in',
  },
  {
    path: 'requests',
    canActivate: [authGuard],
    loadComponent: () => import('./features/requests/request-list').then((m) => m.RequestList),
    title: 'Requests',
  },
  {
    path: 'requests/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/requests/request-form').then((m) => m.RequestForm),
    title: 'New request',
  },
  {
    path: 'requests/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/requests/request-detail').then((m) => m.RequestDetailPage),
    title: 'Request',
  },
  {
    path: 'dashboard',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/dashboard').then((m) => m.Dashboard),
    title: 'Dashboard',
  },
  { path: '**', redirectTo: 'requests' },
];
