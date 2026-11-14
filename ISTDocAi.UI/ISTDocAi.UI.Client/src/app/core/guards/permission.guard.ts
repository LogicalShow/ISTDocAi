import { Injectable, inject } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router, UrlTree } from '@angular/router';
import { Observable } from 'rxjs';
import { PermissionService } from '../services/permission.service';
import { FeedbackService } from '../services/feedback.service';
import { ROUTE_PERMISSIONS } from '../config/route-permissions.config';
import { AuthorizedRouteService } from '../services/authorized-route.service';

/**
 * Route guard to protect routes based on user permissions
 *
 * Permission Format: {PageId}.{ActionId}
 * Example: MD.BP.AGT.VIEW
 *
 * Usage Method 1 - Explicit permission in route data (preferred for clarity):
 * {
 *   path: 'agents',
 *   component: AgentListComponent,
 *   canActivate: [PermissionGuard],
 *   data: { permission: 'MD.BP.AGT.VIEW' }
 * }
 *
 * Usage Method 2 - Auto-detect from ROUTE_PERMISSIONS config:
 * {
 *   path: 'agents',
 *   component: AgentListComponent,
 *   canActivate: [PermissionGuard]
 *   // No data needed - will lookup in ROUTE_PERMISSIONS
 * }
 *
 * For multiple permissions (any):
 * data: { permissions: ['MD.BP.AGT.VIEW', 'MD.BP.AGT.EDIT'], permissionMode: 'any' }
 *
 * For multiple permissions (all):
 * data: { permissions: ['MD.BP.AGT.VIEW', 'MD.BP.AGT.EDIT'], permissionMode: 'all' }
 */
@Injectable({
  providedIn: 'root'
})
export class PermissionGuard implements CanActivate {
  private permissionService = inject(PermissionService);
  private router = inject(Router);
  private feedbackService = inject(FeedbackService);
  private authorizedRouteService = inject(AuthorizedRouteService);


  canActivate(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): Observable<boolean | UrlTree> | Promise<boolean | UrlTree> | boolean | UrlTree {

    // Get permission requirements from route data
    let permission = route.data['permission'] as string | undefined;
    const permissions = route.data['permissions'] as string[] | undefined;
    const permissionMode = route.data['permissionMode'] as 'any' | 'all' || 'any';

    // If no explicit permission in route data, try to lookup from ROUTE_PERMISSIONS config
    if (!permission && !permissions) {
      const routePath = state.url.substring(1); // Remove leading '/'
      permission = ROUTE_PERMISSIONS[routePath];
    }

    let hasPermission: boolean;

    if (permission) {
      // Single permission check
      hasPermission = this.permissionService.hasPermission(permission);
    } else if (permissions && permissions.length > 0) {
      // Multiple permissions check
      if (permissionMode === 'all') {
        hasPermission = this.permissionService.hasAllPermissions(permissions);
      } else {
        hasPermission = this.permissionService.hasAnyPermission(permissions);
      }
    } else {
      // No permission specified and not in config, allow access
      console.warn('[PermissionGuard] No permission specified for route:', state.url);
      return true;
    }
    if (!hasPermission) {
      console.warn('[PermissionGuard] Access denied to route:', state.url);

      this.feedbackService.error(
        'feedback.access.accessDenied.title',
        'feedback.access.accessDenied.message'
      );

      // A completed navigation means the user already has a valid page open.
      // Cancel this navigation so their current context is preserved. During an
      // initial/direct load (including refresh), there is no page to preserve,
      // so redirect to the first route the user can access instead.
      if (this.router.navigated) {
        return false;
      }

      const fallbackRoute =
        this.authorizedRouteService.getLastAccessibleRoute(state.url) ??
        this.authorizedRouteService.getFirstAccessibleRoute(state.url);
      return this.router.parseUrl(fallbackRoute ?? '/');
    }

    return true;
  }
}
