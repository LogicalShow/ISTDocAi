import { Routes } from '@angular/router';

export const iamRoutes: Routes = [
  {
    path: 'roles',
    loadComponent: () =>
      import('./pages/role/role-list/role-list.component')
        .then(m => m.RoleListComponent),
    data: { breadcrumb: 'breadcrumb.iam.roles' }
  },
  {
    path: 'roles/permissions/:id',
    loadComponent: () =>
      import('./pages/role/role-permissions/role-permissions.component')
        .then(m => m.RolePermissionsComponent),
    data: { breadcrumb: 'breadcrumb.iam.rolePermissions' }
  },
  {
    path: 'users',
    loadComponent: () =>
      import('./pages/user/user-list/user-list.component')
        .then(m => m.UserListComponent),
    data: { breadcrumb: 'breadcrumb.iam.users' }
  },
  {
    path: 'users/permissions/:id',
    loadComponent: () =>
      import('./pages/user/user-permissions/user-permissions.component')
        .then(m => m.UserPermissionsComponent),
    data: { breadcrumb: 'breadcrumb.iam.userPermissions' }
  }
];
