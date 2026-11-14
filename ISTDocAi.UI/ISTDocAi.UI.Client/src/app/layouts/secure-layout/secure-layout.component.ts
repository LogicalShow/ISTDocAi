import { Component, DestroyRef, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { RouterOutlet } from '@angular/router';
import { NavigationEnd, Router } from '@angular/router';
import { NzLayoutModule } from 'ng-zorro-antd/layout';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { BreakpointObserver } from '@angular/cdk/layout';
import { filter, map } from 'rxjs/operators';
import { TranslatePipe } from '@ngx-translate/core';
import { UserMenuService } from '../../core/services/user-menu.service';
import { AppStore } from '../../core/state/app.store';
import { RealtimeService } from '../../core/services/realtime.service';
import { FeedbackService } from '../../core/services/feedback.service';
import { UserMenuDto } from '../../core/models/user-menu.model';
import { HeaderComponent } from './header/header.component';
import { SidebarComponent } from './sidebar/sidebar.component';
import { AuthorizedRouteService } from '../../core/services/authorized-route.service';
import { ServerNotification } from '../../core/models/server-notification.model';
import { SidebarChildItem, SidebarItem } from '../../core/models/sidebar-item.model';

type SidebarNavigationItem = SidebarItem | SidebarChildItem;

@Component({
  selector: 'app-secure-layout-component',
  templateUrl: './secure-layout.component.html',
  styleUrl: './secure-layout.component.css',
  imports: [
    RouterOutlet,
    NzLayoutModule,
    NzBreadCrumbModule,
    TranslatePipe,
    HeaderComponent,
    SidebarComponent
],
})
export class SecureLayoutComponent implements OnInit {
  private appStore = inject(AppStore);
  private router = inject(Router);
  private userMenuService = inject(UserMenuService);
  private realtimeService = inject(RealtimeService);
  private feedbackService = inject(FeedbackService);
  private destroyRef = inject(DestroyRef);
  private breakpointObserver = inject(BreakpointObserver);
  private authorizedRouteService = inject(AuthorizedRouteService);

  /** Matches the sider's own nzBreakpoint="md" (ng-zorro's md is 768px). */
  private static readonly NARROW_VIEWPORT = '(max-width: 767.98px)';

  readonly sidebarIsCollapsed = signal(true);
  private viewportIsNarrow = false;
  readonly isMobile = signal(false);
  readonly mobileSidebarOpen = signal(false);
  private mobileStateInitialized = false;
  notifications: ServerNotification[] | null = [];
  readonly breadcrumbs = signal<{ label: string; url?: string }[]>([]);
  // Derived from the store (not copied), so a menu reloaded elsewhere - e.g. by
  // PermissionSyncService after a live permission change - updates the sidebar immediately.
  readonly sidebarItems = computed<SidebarItem[]>(() => this.appStore.snapshot().localizedUserMenu ?? []);
  showWelcomeScreen = true;

  constructor() {
    // Breadcrumb labels come from the menu: rebuild them whenever the menu changes.
    effect(() => {
      this.sidebarItems();
      untracked(() => this.updateBreadcrumbs(this.router.url));
    });
  }

  //ngOnInit(): void {
  //  setTimeout(() => {
  //    this.sidebarIsCollapsed = true;
  //  });
  //  let userInfo = this.appStore.getState().userInfo;
  //  if (userInfo) {
  //    this.username = userInfo.fullName;
  //    this.email = userInfo.email;
  //  }

  //  // init breadcrumbs with current route
  //  const currentRoute = this.router.url;
  //  this.breadcrumbs = this.buildBreadcrumbsForUrl(currentRoute);

  //  // subscribe to the routing changes to set breadcrumbs
  //  this.router.events
  //    .pipe(
  //      filter(event => event instanceof NavigationEnd),
  //      map(() => this.buildBreadcrumbs())
  //    )
  //    .subscribe(breadcrumbs => (this.breadcrumbs = breadcrumbs));

  //  this.subscription = this.realtimeService.getServerNotifications()
  //    .subscribe(serverNotifications => {
  //      // process notifications
  //      for (const notification of serverNotifications || []) {
  //        if (notification) {
  //          if (notification.Type === 'Online') {
  //            this.feedbackService.info('Online', notification.Message);
  //          } else {
  //            this.notifications?.unshift(notification);
  //          }
  //        }
  //      }
  //      // refresh notifications count
  //      this.notificationsCount = this.notifications?.length || 0;
  //    });


  //}

  /**
   * The sider stays collapsed only when there is no room for it (narrow
   * viewport). Navigating between tabs never forces it open or closed -
   * only the header's collapse trigger (toggleSidebar) changes that.
   */
  private applySidebarState(): void {
    if (this.viewportIsNarrow) {
      this.sidebarIsCollapsed.set(true);
    }
  }

  ngOnInit(): void {
    this.reloadSidebarItems();

    this.breakpointObserver
      .observe(['(max-width: 767px)'])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        this.isMobile.set(result.matches);
        if (result.matches) {
          this.sidebarIsCollapsed.set(true);
          if (!this.mobileStateInitialized) {
            this.mobileSidebarOpen.set(false);
          }
        } else {
          this.mobileSidebarOpen.set(false);
        }
        this.mobileStateInitialized = true;
      });

    // The sider carries nzBreakpoint="md", but this used to expand it back
    // unconditionally right after load - which is why the 256px menu reappeared
    // on phones and squeezed the page. Drive it from the viewport and the route
    // instead: narrow screens always keep the rail, and the dashboard starts
    // collapsed so its widgets get the full width.
    this.breakpointObserver
      .observe(SecureLayoutComponent.NARROW_VIEWPORT)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(state => {
        this.viewportIsNarrow = state.matches;
        this.applySidebarState();
      });

    // init breadcrumbs with current route
    this.updateBreadcrumbs(this.router.url);

    // subscribe to the routing changes to set breadcrumbs
    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        map((event: NavigationEnd) => event.urlAfterRedirects),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(url => {
        this.authorizedRouteService.rememberAccessibleRoute(url);
        this.updateBreadcrumbs(url);
        this.applySidebarState();
      });

    this.realtimeService.getServerNotifications()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(serverNotifications => {
        for (const notification of serverNotifications || []) {
          if (notification) {
            if (notification.Type === 'Online') {
              this.feedbackService.apiInfo(notification.Type, notification.Message);
            } else {
              this.notifications?.unshift(notification);
            }
          }
        }
      });
  }

  private reloadSidebarItems(): void {
    const userMenuSource = this.appStore.getState().userMenuSource;
    if (userMenuSource) {
      this.applyMenu(userMenuSource, false);
      return;
    }

    this.userMenuService.getUserMenu().subscribe({
      next: userMenuSource => {
        this.applyMenu(userMenuSource, true);
      },
      error: error => {
        console.error('Failed to load sidebar menu', error);
      }
    });
  }

  private applyMenu(userMenuSource: UserMenuDto, storeUserMenuSource: boolean): void {
    const localizedMenu = this.userMenuService.localizeMenu(userMenuSource);
    // sidebarItems and breadcrumbs follow the store.
    if (storeUserMenuSource) {
      this.appStore.setUserMenu(userMenuSource, localizedMenu);
    } else {
      this.appStore.updateLocalizedUserMenu(localizedMenu);
    }
  }

  // Resolve the existing menu source with TranslationService's current
  // culture without another HTTP request.
  syncSidebarFromStore(): void {
    const userMenuSource = this.appStore.getState().userMenuSource;
    if (userMenuSource) {
      this.applyMenu(userMenuSource, false);
    }
  }

  private updateBreadcrumbs(url: string) {
    this.breadcrumbs.set(this.buildBreadcrumbsForUrl(url));
  }

  //private buildBreadcrumbs(): Array<{ label: string; url?: string }> {
  //  const breadcrumbs: Array<{ label: string; url?: string }> = [];

  //  const findBreadcrumbs = (items: any[], url: string): boolean => {
  //    for (const item of items) {
  //      if (item.url === url) {
  //        breadcrumbs.unshift({ label: item.label, url: item.url });
  //        return true;
  //      }
  //      if (item.children && findBreadcrumbs(item.children, url)) {
  //        breadcrumbs.unshift({ label: item.title });
  //        return true;
  //      }
  //    }
  //    return false;
  //  };

  //  const currentUrl = this.router.url;
  //  findBreadcrumbs(this.sidebarItems, currentUrl);

  //  return breadcrumbs;
  //}
  private buildBreadcrumbs(): { label: string; url?: string }[] {
    const breadcrumbs: { label: string; url?: string }[] = [];

    const findBreadcrumbs = (items: SidebarNavigationItem[], url: string): boolean => {
      for (const item of items) {
        const label = 'label' in item ? item.label : item.title;
        if (item.url === url) {
          breadcrumbs.unshift({ label, url: item.url });
          return true;
        }
        if (item.children && findBreadcrumbs(item.children, url)) {
          breadcrumbs.unshift({ label, url: item.url ?? undefined });
          return true;
        }
      }
      return false;
    };

    const currentUrl = this.router.url.split('?')[0]; 
    findBreadcrumbs(this.sidebarItems(), currentUrl);

    return breadcrumbs.filter(b => b.label && b.label.trim() !== '');
  }

  //private buildBreadcrumbsForUrl(url: string): Array<{ label: string; url?: string }> {
  //  const breadcrumbs: Array<{ label: string; url?: string }> = [];

  //  const findBreadcrumbs = (items: any[], targetUrl: string): boolean => {
  //    for (const item of items) {
  //      if (item.url === targetUrl) {
  //        breadcrumbs.unshift({ label: item.label, url: item.url });
  //        return true;
  //      }
  //      if (item.children && findBreadcrumbs(item.children, targetUrl)) {
  //        breadcrumbs.unshift({ label: item.title });
  //        return true;
  //      }
  //    }
  //    return false;
  //  };

  //  findBreadcrumbs(this.sidebarItems, url);

  //  return breadcrumbs;
  //}
  private buildBreadcrumbsForUrl(url: string): { label: string; url?: string }[] {
    const breadcrumbs: { label: string; url?: string }[] = [];

    const cleanUrl = url.split('?')[0];
    const permissionRoute = cleanUrl.match(/^\/iam\/(roles|users)\/permissions\/[^/]+\/?$/);
    const breadcrumbTargetUrl = permissionRoute ? `/iam/${permissionRoute[1]}` : cleanUrl;

    const findBreadcrumbs = (items: SidebarNavigationItem[], targetUrl: string): boolean => {
      for (const item of items) {
        const label = 'label' in item ? item.label : item.title;
        if (item.url === targetUrl) {
          breadcrumbs.unshift({ label, url: item.url });
          return true;
        }
        if (item.children && findBreadcrumbs(item.children, targetUrl)) {
          breadcrumbs.unshift({ label, url: item.url ?? undefined });
          return true;
        }
      }
      return false;
    };

    findBreadcrumbs(this.sidebarItems(), breadcrumbTargetUrl);

    if (permissionRoute) {
      breadcrumbs.push({
        label: permissionRoute[1] === 'roles'
          ? 'breadcrumb.iam.rolePermissions'
          : 'breadcrumb.iam.userPermissions'
      });
    }

    return breadcrumbs.filter(b => b.label && b.label.trim() !== '');
  }

  onSiderCollapsedChange(collapsed: boolean): void {
    if (!this.isMobile()) {
      this.sidebarIsCollapsed.set(collapsed);
    }
  }

  toggleSidebar(): void {
    if (this.isMobile()) {
      this.mobileSidebarOpen.update(open => !open);
    } else {
      this.sidebarIsCollapsed.update(collapsed => !collapsed);
    }
  }

  closeMobileSidebar(): void {
    this.mobileSidebarOpen.set(false);
  }

  scrollToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' });
    if (this.isMobile()) {
      this.mobileSidebarOpen.set(false);
    }
  }

}
