import { Injectable, inject } from '@angular/core';
import { ROUTE_PERMISSIONS } from '../config/route-permissions.config';
import { SidebarChildItem, SidebarItem } from '../models/sidebar-item.model';
import { AppStore } from '../state/app.store';
import { PermissionService } from './permission.service';

type NavigableMenuItem = Pick<SidebarChildItem, 'url' | 'children' | 'openInNewTab'>;

@Injectable({ providedIn: 'root' })
export class AuthorizedRouteService {
  private appStore = inject(AppStore);
  private permissionService = inject(PermissionService);

  private readonly lastAccessibleRouteStorageKey = 'lastAccessibleRoute';

  getFirstAccessibleRoute(excludedUrl?: string): string | null {
    const menuItems = this.getMenuItems();
    const menuRoute = this.findFirstInternalRoute(menuItems, excludedUrl);

    if (menuRoute) {
      return menuRoute;
    }

    return this.findFirstRouteFromPermissions(excludedUrl);
  }

  rememberAccessibleRoute(url: string): void {
    const route = this.normalizeInternalRoute(url);
    if (route && route !== '/' && this.hasRequiredPermission(route)) {
      sessionStorage.setItem(this.lastAccessibleRouteStorageKey, route);
    }
  }

  getLastAccessibleRoute(excludedUrl?: string): string | null {
    const route = this.normalizeInternalRoute(
      sessionStorage.getItem(this.lastAccessibleRouteStorageKey) ?? undefined
    );

    if (
      !route ||
      this.isSameRoute(route, excludedUrl) ||
      !this.hasRequiredPermission(route)
    ) {
      return null;
    }

    return route;
  }

  private getMenuItems(): SidebarItem[] {
    return this.appStore.getState().localizedUserMenu ?? [];
  }

  private findFirstInternalRoute(
    items: NavigableMenuItem[],
    excludedUrl?: string
  ): string | null {
    for (const item of items) {
      const route = this.normalizeInternalRoute(item.url);

      if (
        route &&
        !item.openInNewTab &&
        this.hasRequiredPermission(route) &&
        !this.isSameRoute(route, excludedUrl)
      ) {
        return route;
      }

      const childRoute = this.findFirstInternalRoute(item.children ?? [], excludedUrl);
      if (childRoute) {
        return childRoute;
      }
    }

    return null;
  }

  private findFirstRouteFromPermissions(excludedUrl?: string): string | null {
    for (const [route, permission] of Object.entries(ROUTE_PERMISSIONS)) {
      // Parameterized edit/detail routes are not valid landing pages.
      if (route.includes(':') || !this.permissionService.hasPermission(permission)) {
        continue;
      }

      const normalizedRoute = `/${route}`;
      if (!this.isSameRoute(normalizedRoute, excludedUrl)) {
        return normalizedRoute;
      }
    }

    return null;
  }

  private normalizeInternalRoute(url?: string): string | null {
    const trimmedUrl = url?.trim();
    if (!trimmedUrl || /^(?:[a-z][a-z\d+.-]*:)?\/\//i.test(trimmedUrl)) {
      return null;
    }

    return trimmedUrl.startsWith('/') ? trimmedUrl : `/${trimmedUrl}`;
  }

  private hasRequiredPermission(route: string): boolean {
    const routePath = route.split(/[?#]/, 1)[0].replace(/^\//, '').replace(/\/$/, '');
    const permission = ROUTE_PERMISSIONS[routePath];
    return !permission || this.permissionService.hasPermission(permission);
  }

  private isSameRoute(route: string, otherUrl?: string): boolean {
    if (!otherUrl) {
      return false;
    }

    const normalize = (url: string) => url.split(/[?#]/, 1)[0].replace(/\/$/, '') || '/';
    return normalize(route) === normalize(otherUrl);
  }
}
