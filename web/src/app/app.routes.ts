import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth';
import { appDataGuard } from './core/meta.service';

export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], title: 'Sign in · Job Search', loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  {
    path: '',
    canActivate: [authGuard, appDataGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', title: 'Dashboard · Job Search', loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.DashboardPage) },
      { path: '**', redirectTo: '' },
    ],
  },
];
