import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { EMPTY, Observable, catchError, debounceTime, switchMap, tap } from 'rxjs';
import { ROUTE_PERMISSIONS } from '../config/route-permissions.config';
import { AppStore } from '../state/app.store';
import { AuthorizedRouteService } from './authorized-route.service';
import { FeedbackService } from './feedback.service';
import { PermissionService } from './permission.service';
import { RealtimeService } from './realtime.service';
import { UserMenuService } from './user-menu.service';

/**
 * Keeps the client's permissions in sync with the server-side permission cache.
 *
 * The server pushes "PermissionsChanged" over SignalR whenever the signed-in user's
 * permissions are modified (and the hub reconnect is treated the same way, in case an
 * event was missed while offline). On each event the permissions and menu are reloaded,
 * and the user is moved off the current page if they no longer have access to it —
 * no logout/login required.
 */
@Injectable({ providedIn: 'root' })
export class PermissionSyncService {
  private realtimeService = inject(RealtimeService);
  private permissionService = inject(PermissionService);
  private userMenuService = inject(UserMenuService);
  private appStore = inject(AppStore);
  private router = inject(Router);
  private authorizedRouteService = inject(AuthorizedRouteService);
  private feedbackService = inject(FeedbackService);

  constructor() {
    this.realtimeService.onPermissionsChanged().pipe(
      debounceTime(300),
      // Errors are handled per event so one failed reload does not end the subscription.
      switchMap(() => this.refresh().pipe(catchError(() => EMPTY)))
    ).subscribe();
  }

  private refresh(): Observable<unknown> {
    return this.permissionService.load().pipe(
      switchMap(() => this.userMenuService.getUserMenu()),
      tap(userMenuSource => {
        const localizedMenu = this.userMenuService.localizeMenu(userMenuSource);
        this.appStore.setUserMenu(userMenuSource, localizedMenu);
        this.ensureCurrentRouteAllowed();
      })
    );
  }

  private ensureCurrentRouteAllowed(): void {
    const segments = this.router.url.split(/[?#]/, 1)[0].replace(/^\//, '').replace(/\/$/, '').split('/');
    const requiredPermission = Object.entries(ROUTE_PERMISSIONS).find(([route]) => {
      const routeSegments = route.split('/');
      return routeSegments.length === segments.length &&
        routeSegments.every((part, i) => part.startsWith(':') || part === segments[i]);
    })?.[1];

    if (!requiredPermission || this.permissionService.hasPermission(requiredPermission)) {
      return;
    }

    const fallbackRoute = this.authorizedRouteService.getFirstAccessibleRoute(this.router.url) ?? '/';
    void this.router.navigateByUrl(fallbackRoute);
    this.feedbackService.warning(
      'feedback.access.permissionsChanged.title',
      'feedback.access.permissionsChanged.message'
    );
  }
}
