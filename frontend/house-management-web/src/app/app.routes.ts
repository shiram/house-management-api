import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell.component';
import { LoginComponent } from './features/login/login.component';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    component: ShellComponent,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent), canActivate: [authGuard] }
    ]
  },
  { path: 'login', component: LoginComponent },
  { path: '**', redirectTo: '' }
];
