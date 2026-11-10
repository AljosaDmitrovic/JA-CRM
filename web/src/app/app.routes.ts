import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth';
import { appDataGuard } from './core/meta.service';
import { unsavedChangesGuard } from './core/unsaved-changes';

const title = (page: string) => `${page} · Job Search`;

export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], title: title('Sign in'), loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  {
    path: '',
    canActivate: [authGuard, appDataGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', title: title('Dashboard'), loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.DashboardPage) },
      {
        path: 'applications',
        children: [
          { path: '', title: title('Applications'), loadComponent: () => import('./features/applications/applications-page').then((m) => m.ApplicationsPage) },
          { path: ':id', title: title('Application'), loadComponent: () => import('./features/applications/application-detail').then((m) => m.ApplicationDetailPage) },
          { path: ':id/edit', title: title('Edit application'), canDeactivate: [unsavedChangesGuard], loadComponent: () => import('./features/applications/application-edit').then((m) => m.ApplicationEditPage) },
        ],
      },
      {
        path: 'companies',
        children: [
          { path: '', title: title('Companies'), loadComponent: () => import('./features/companies/companies-page').then((m) => m.CompaniesPage) },
          { path: ':id', title: title('Company'), loadComponent: () => import('./features/companies/company-detail').then((m) => m.CompanyDetailPage) },
        ],
      },
      {
        path: 'vacancies',
        children: [
          { path: '', title: title('Vacancies'), loadComponent: () => import('./features/vacancies/vacancies-page').then((m) => m.VacanciesPage) },
          { path: 'new', title: title('New vacancy'), canDeactivate: [unsavedChangesGuard], loadComponent: () => import('./features/vacancies/vacancy-form').then((m) => m.VacancyFormPage) },
          { path: ':id', title: title('Vacancy'), loadComponent: () => import('./features/vacancies/vacancy-detail').then((m) => m.VacancyDetailPage) },
          { path: ':id/edit', title: title('Edit vacancy'), canDeactivate: [unsavedChangesGuard], loadComponent: () => import('./features/vacancies/vacancy-form').then((m) => m.VacancyFormPage) },
        ],
      },
      { path: 'settings', title: title('Settings'), loadComponent: () => import('./features/settings/settings-page').then((m) => m.SettingsPage) },
      { path: '**', redirectTo: '' },
    ],
  },
];
