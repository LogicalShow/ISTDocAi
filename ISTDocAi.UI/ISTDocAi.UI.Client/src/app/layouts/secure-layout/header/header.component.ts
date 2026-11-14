import { Component, DestroyRef, OnInit, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { Router } from '@angular/router';
import { NzLayoutModule } from 'ng-zorro-antd/layout';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { AppStore } from '../../../core/state/app.store';
import { RealtimeService } from '../../../core/services/realtime.service';
import { FeedbackService } from '../../../core/services/feedback.service';
import { ServerNotification, ServerNotificationUserDetail } from '../../../core/models/server-notification.model';
import { NotificationUserDetailService } from '../../../modules/public/services/notification-user-detail.service';
import { NotificationUserService } from '../../../modules/public/services/notification-user.service';
import { ChangeCurrentUserPasswordComponent } from '../../../modules/auth/pages/change-current-user-password/change-current-user-password.component';
import { IconComponent } from '../../../shared/icons/icon.component';
import { LanguageSwitcherComponent } from '../../../shared/components/language-switcher/language-switcher.component';
import { ThemeSwitcherComponent } from '../../../shared/components/theme-switcher/theme-switcher.component';
import { TranslationService } from '../../../core/services/translation.service';
import { Culture } from '../../../core/constants/culture.constants';

@Component({
  selector: 'app-secure-header',
  templateUrl: './header.component.html',
  styleUrl: './header.component.css',
  imports: [
    NzLayoutModule,
    NzAvatarModule,
    NzBadgeModule,
    NzPopoverModule,
    NzListModule,
    NzButtonModule,
    NzSpinModule,
    TranslatePipe,
    IconComponent,
    LanguageSwitcherComponent,
    ThemeSwitcherComponent
],
})
export class HeaderComponent implements OnInit {
  private authService = inject(AuthService);
  private appStore = inject(AppStore);
  private router = inject(Router);
  private realtimeService = inject(RealtimeService);
  private feedbackService = inject(FeedbackService);
  private notificationUserService = inject(NotificationUserService);
  private notificationUserDetailService = inject(NotificationUserDetailService);
  private modal = inject(NzModalService);
  private destroyRef = inject(DestroyRef);
  private translationService = inject(TranslationService);

  readonly isMobile = input(false);
  readonly sidebarIsCollapsed = input(true);
  readonly mobileSidebarOpen = input(false);

  readonly toggleSidebar = output<void>();
  readonly languageChanged = output<void>();

  readonly notificationsVisible = signal(false);
  readonly notificationsCount = signal(0);
  notifications: ServerNotification[] | null = [];
  readonly userInfoVisible = signal(false);
  readonly username = signal('');
  readonly email = signal('');

  // Pagination state for Facebook-style notifications
  readonly notificationsDto = signal<ServerNotificationUserDetail[]>([]);
  readonly activeTab = signal<'all' | 'unread'>('all');
  private currentPage = 1;
  private readonly pageSize = 10;
  private totalCount = 0;
  readonly hasMore = signal(true);
  readonly isLoadingMore = signal(false);

  ngOnInit(): void {
    // Load initial page of notifications
    this.loadNotificationsPage(true);

    // SignalR: when server pushes refreshed notifications (e.g. after mark-as-read from another tab)
    // Reload using the current tab's filter instead of blindly replacing with unfiltered data
    this.realtimeService.onOldNotifications()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadNotificationsPage(true));

    // SignalR: real-time new notification
    this.realtimeService.onNewNotification()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(n => {
        // Deduplicate: user may receive from both ntf: group and usr: group
        const exists = this.notificationsDto().some(
          x => x.CorrelationId === n.CorrelationId && x.NotificationId === n.NotificationId
        );
        if (!exists) {
          this.notificationsDto.update(items => [n, ...items]);
          this.totalCount++;
          this.updateNotificationsCount();
        }
      });

    this.realtimeService.onSubscriptionsChanged()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        console.log('Subscriptions changed (server forced refresh).');
      });

    const userInfo = this.appStore.getState().userInfo;
    if (userInfo) {
      this.username.set(userInfo.fullName);
      this.email.set(userInfo.email);
    }

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

  // ── Notification pagination methods ──

  switchTab(tab: 'all' | 'unread'): void {
    if (this.activeTab() === tab) return;
    this.activeTab.set(tab);
    this.notificationsDto.set([]);
    this.currentPage = 1;
    this.hasMore.set(true);
    this.loadNotificationsPage(true);
  }

  loadNotificationsPage(reset = false): void {
    if (this.isLoadingMore()) return;
    this.isLoadingMore.set(true);

    const page = reset ? 1 : this.currentPage;
    this.notificationUserService
      .getUserNotificationsPaginated(page, this.pageSize, this.activeTab() === 'unread')
      .subscribe({
        next: (result) => {
          const items = result.Items || [];
          this.totalCount = result.TotalCount;
          if (reset) {
            this.notificationsDto.set(items);
            this.currentPage = 1;
          } else {
            this.notificationsDto.update(current => [...current, ...items]);
          }
          this.hasMore.set(this.notificationsDto().length < this.totalCount);
          this.updateNotificationsCount();
          this.isLoadingMore.set(false);
        },
        error: (error) => {
          this.feedbackService.handleApiError(error);
          this.isLoadingMore.set(false);
        }
      });
  }

  loadMore(): void {
    if (!this.hasMore() || this.isLoadingMore()) return;
    this.currentPage++;
    this.loadNotificationsPage(false);
  }

  onNotificationScroll(event: Event): void {
    const el = event.target as HTMLElement;
    const threshold = 50;
    if (el.scrollHeight - el.scrollTop - el.clientHeight < threshold) {
      this.loadMore();
    }
  }

  private updateNotificationsCount(): void {
    this.notificationsCount.set(this.notificationsDto().filter(
      x => x.ReceivedNotificationStatus === 1
    ).length);
  }

  markNotificationAsRead(item: ServerNotificationUserDetail) {
    const targetUrl = item.TargetUrl && item.TargetId ? `${item.TargetUrl}${item.TargetId}` : null;
    if (item.ReceivedNotificationStatus === 1) {
    this.notificationUserDetailService.markAsRead({
      NotificationId: item.NotificationId,
      CorrelationId: item.CorrelationId
    }).subscribe({
      next: () => {
        const idx = this.notificationsDto().findIndex(
          n => n.CorrelationId === item.CorrelationId && n.NotificationId === item.NotificationId
        );
        if (idx !== -1) {
          if (this.activeTab() === 'unread') {
            // Remove from unread list since it's now read
            this.notificationsDto.update(items => items.filter((_, i) => i !== idx));
            this.totalCount = Math.max(0, this.totalCount - 1);
            this.hasMore.set(this.notificationsDto().length < this.totalCount);
          } else {
            // Update status for "All" tab
            this.notificationsDto.update(items =>
              items.map((n, i) => i === idx ? { ...n, ReceivedNotificationStatus: 2 } : n)
            );
          }
        }
        this.updateNotificationsCount();

        if (targetUrl) {
          this.router.navigate([item.TargetUrl, item.TargetId]);
        }
      },
      error: (error) => {
        this.feedbackService.handleApiError(error);
        if (targetUrl) {
          this.router.navigate([item.TargetUrl, item.TargetId]);
        }
      }
    });} else if (targetUrl) {
      this.router.navigate([item.TargetUrl, item.TargetId]);
    }
  }

  changeReadStatus(): void {
    this.updateNotificationsCount();
  }

  clickMe(): void {
    this.userInfoVisible.set(false);
  }

  logout() {
    this.authService.logout();
  }

  openChangePassword() {
    this.userInfoVisible.set(false);
    this.modal.create({
      nzContent: ChangeCurrentUserPasswordComponent,
      nzClassName: 'app-change-password-modal',
      nzWidth: 485,
      nzCentered: true,
      nzClosable: false,
      nzFooter: null,
      nzMaskClosable: false
    });
  }

  currentCulture(): Culture {
    return this.translationService.culture();
  }

}
