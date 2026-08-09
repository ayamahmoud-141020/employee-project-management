import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards/auth.guard';

/**
 * Every feature is lazy-loaded, so the login page ships without the tables, dialogs and
 * charts nobody has asked for yet.
 */
export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · EPM',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · EPM',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'employees',
        title: 'Employees · EPM',
        loadComponent: () =>
          import('./features/employees/employee-list.component').then((m) => m.EmployeeListComponent),
      },
      {
        path: 'departments',
        title: 'Departments · EPM',
        // Admin-only, matching the API's CanManageDepartments policy. Managers and Users can
        // still see department names through the employees list; this is the editing screen.
        canActivate: [roleGuard('Admin')],
        loadComponent: () =>
          import('./features/departments/department-list.component').then(
            (m) => m.DepartmentListComponent,
          ),
      },
      {
        path: 'projects',
        title: 'Projects · EPM',
        loadComponent: () =>
          import('./features/projects/project-list.component').then((m) => m.ProjectListComponent),
      },
      {
        path: 'projects/:id',
        title: 'Project · EPM',
        loadComponent: () =>
          import('./features/projects/project-detail.component').then(
            (m) => m.ProjectDetailComponent,
          ),
      },
      {
        path: 'my-assignments',
        title: 'My assignments · EPM',
        loadComponent: () =>
          import('./features/assignments/my-assignments.component').then(
            (m) => m.MyAssignmentsComponent,
          ),
      },
    ],
  },
  // Catch-all last: an unknown URL should land somewhere useful rather than on a blank page.
  { path: '**', redirectTo: '' },
];
