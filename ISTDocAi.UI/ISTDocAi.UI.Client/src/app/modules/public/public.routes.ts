import { Routes } from '@angular/router';

export const publicRoutes: Routes = [
  {
    path: 'setting',
    loadComponent: () => import('./pages/setting/setting.component')
      .then(m => m.SettingComponent),
    data: { breadcrumb: 'breadcrumb.public.settings' }
  },
  {
    path: 'notificationSettings',
    loadComponent: () => import('./pages/notification-setting/notification-list/notification-list.component')
      .then(m => m.NotificationListComponent),
    data: { breadcrumb: 'breadcrumb.public.notificationSettings' }
  }
];
