import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth-guard';
import { adminGuard } from './core/guards/admin-guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'chat',
    loadComponent: () =>
      import('./features/chat/chat/chat').then((m) => m.Chat),
    canActivate: [authGuard],
  },
  {
    path: 'account',
    loadComponent: () =>
      import('./features/account/account/account').then((m) => m.Account),
    canActivate: [authGuard],
  },
  {
    path: 'admin',
    loadComponent: () =>
      import('./features/admin/users/users').then((m) => m.AdminUsers),
    canActivate: [authGuard, adminGuard],
  },
  {
    path: 'admin/users',
    redirectTo: 'admin',
    pathMatch: 'full',
  },
  { path: '**', redirectTo: 'login' },
];
