import { Injectable, signal, inject } from '@angular/core';
import { CookieService } from 'ngx-cookie-service';
import { Subject } from 'rxjs';
import { Culture, isSupportedCulture } from '../constants/culture.constants';
import { HostInfo } from '../models/HostInfo.model';
import { UserInfo } from '../models/UserInfo.model';
import { SidebarItem } from '../models/sidebar-item.model';
import { AppStoreState } from '../models/app-store-state.model';
import { UserMenuDto } from '../models/user-menu.model';

export const initialState: AppStoreState = {
  hostInfo: {
    uiLanguage: 'en',
    utcOffset: 2,
  },
  userInfo: {
    fullName: '',
    email: ''
  },
  connectionId: null,
  userMenuSource: null,
  localizedUserMenu: null,
};

@Injectable({
  providedIn: 'root',
})
export class AppStore {
  private cookieService = inject(CookieService);

  private state = signal<AppStoreState>({
    ...initialState,
    hostInfo: { ...initialState.hostInfo },
    userInfo: { ...initialState.userInfo }
  });
  private readonly cultureChangedSource = new Subject<Culture>();

  readonly snapshot = this.state.asReadonly();
  readonly cultureChanged$ = this.cultureChangedSource.asObservable();

  constructor() {
    const restoredState: AppStoreState = {
      ...initialState,
      hostInfo: { ...initialState.hostInfo },
      userInfo: { ...initialState.userInfo }
    };

    const savedHostInfo = this.cookieService.get('hostInfo');
    if (savedHostInfo) {
      try {
        const parsedHostInfo: Partial<HostInfo> = JSON.parse(savedHostInfo);
        restoredState.hostInfo = {
          ...restoredState.hostInfo,
          ...parsedHostInfo
        };
      } catch (error) {
        console.error('Failed to parse hostInfo from cookies:', error);
      }
    }

    const savedUserInfo = this.cookieService.get('userInfo');
    if (savedUserInfo) {
      try {
        const parsedUserInfo: UserInfo = JSON.parse(savedUserInfo);
        restoredState.userInfo = parsedUserInfo;
      } catch (error) {
        console.error('Failed to parse userInfo from cookies:', error);
      }
    }

    this.state.set(restoredState);

    // Remove menu cookies created by older versions. The raw menu source is
    // retained only in memory for the current application session.
    this.cookieService.delete('userMenu');
    this.cookieService.delete('userMenuCulture');
    sessionStorage.removeItem('user_menu');
    localStorage.removeItem('user_menu');
    localStorage.removeItem('user_menu_culture');
  }

  getState(): AppStoreState {
    return this.state();
  }

  updateHostInfo(hostInfo: Partial<HostInfo>): void {
    const currentState = this.state();
    const previousCulture = currentState.hostInfo.uiLanguage;
    const updatedHostInfo = { ...currentState.hostInfo, ...hostInfo };
    this.state.set({ ...currentState, hostInfo: updatedHostInfo });

    this.cookieService.set('hostInfo', JSON.stringify(updatedHostInfo));

    if (updatedHostInfo.uiLanguage !== previousCulture && isSupportedCulture(updatedHostInfo.uiLanguage)) {
      this.cultureChangedSource.next(updatedHostInfo.uiLanguage);
    }
  }

  updateUserInfo(userInfo: Partial<UserInfo>): void {
    const currentState = this.state();
    const updatedUserInfo = { ...currentState.userInfo, ...userInfo };
    this.state.set({ ...currentState, userInfo: updatedUserInfo });

    this.cookieService.set('userInfo', JSON.stringify(updatedUserInfo));
  }

  updateConnectionId(connectionId: string | null): void {
    const currentState = this.state();
    this.state.set({ ...currentState, connectionId });
  }

  setUserMenu(userMenuSource: UserMenuDto, localizedUserMenu: SidebarItem[]): void {
    const currentState = this.state();
    this.state.set({ ...currentState, userMenuSource, localizedUserMenu });
  }

  updateLocalizedUserMenu(localizedUserMenu: SidebarItem[]): void {
    const currentState = this.state();
    this.state.set({ ...currentState, localizedUserMenu });
  }

  clearUserMenu(): void {
    const currentState = this.state();
    this.state.set({ ...currentState, userMenuSource: null, localizedUserMenu: null });
    this.cookieService.delete('userMenu');
    this.cookieService.delete('userMenuCulture');
  }
}
