import { inject, NgModule } from '@angular/core';
import { CanActivateFn, Router, RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './core/guards/auth.guard';
import { authRoutes } from './modules/auth/auth.routes';
import { publicRoutes } from './modules/public/public.routes';
import { iamRoutes } from './modules/iam/iam.routes';
import { AuthorizedRouteService } from './core/services/authorized-route.service';
import { FeedbackService } from './core/services/feedback.service';

const redirectToFirstAccessibleRoute: CanActivateFn = () => {
  const route = inject(AuthorizedRouteService).getFirstAccessibleRoute();
  if (route) {
    return inject(Router).parseUrl(route);
  }
  inject(FeedbackService).error(
    'feedback.access.accessDenied.title',
    'feedback.access.accessDenied.message'
  );
  return true;
};

export const routes: Routes = [
  ...authRoutes,

  {
    path: '',
    canActivate: [AuthGuard],
    loadComponent: () =>
      import('./layouts/secure-layout/secure-layout.component')
        .then(m => m.SecureLayoutComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        canActivate: [redirectToFirstAccessibleRoute],
        children: []
      },
      { path: 'public', children: publicRoutes },
      { path: 'Administration', children: publicRoutes },
      { path: 'iam', children: iamRoutes },
    ]
  },

  {
    path: '**',
    loadComponent: () =>
      import('./shared/pages/not-found/not-found.component')
        .then(m => m.NotFoundComponent),
    data: { breadcrumb: 'breadcrumb.notFound' }
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
