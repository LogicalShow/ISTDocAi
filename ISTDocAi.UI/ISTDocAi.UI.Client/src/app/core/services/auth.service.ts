import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of, Subscription } from 'rxjs';
import { catchError, tap, switchMap, map } from 'rxjs/operators';
import { AppStore } from '../state/app.store';
import { RealtimeService } from './realtime.service';
import { UserMenuService } from './user-menu.service';
import { ServerNotificationUserDetail } from '../models/server-notification.model';
import { PermissionService } from './permission.service';
import {
  ChangeCurrentUserPasswordCommandResponse,
  LoginCommandResponse,
  ResetPasswordCommandResponse,
  SendOtpEmailCommandResponse,
  SendOtpToCurrentUserCommandResponse,
  VerifyOtpCommandResponse
} from '../models/auth.model';
import { ApiHttpService } from './api-http.service';
import { PermissionSyncService } from './permission-sync.service';

const authTokenName = 'authToken';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
private http = inject(ApiHttpService);
private router = inject(Router);
private appStore = inject(AppStore);
private realtimeService = inject(RealtimeService);
private userMenuService = inject(UserMenuService);
private permissionService = inject(PermissionService);

private sub = new Subscription();
 notificationsDto: ServerNotificationUserDetail[] = [];
  constructor() { 
    // Start listening for live permission changes pushed by the server.
    inject(PermissionSyncService);

    this.sub.add(this.realtimeService.onOldNotifications().subscribe(items => {
    this.notificationsDto = items || [];
    console.log('Received old notifications:',this.notificationsDto);
  }));

  this.sub.add(this.realtimeService.onNewNotification().subscribe(n => {
    this.notificationsDto = [n, ...this.notificationsDto];
    console.log('Received new notification:',this.notificationsDto);
  }));

  this.sub.add(this.realtimeService.onSubscriptionsChanged().subscribe(() => {
    console.log('Subscriptions changed (server forced refresh).');
  }));
  }

  private apiUrl = '/api/iam/auth';


  login(
    username: string,
    password: string,
    remember = false
  ): Observable<LoginCommandResponse> {
    return this.http.post<LoginCommandResponse>(
      `${this.apiUrl}/login`,
      { username, password })
      .pipe(
        switchMap(response => this.completeLoginSession(response, remember))
      );
  }

  private completeLoginSession(
    response: LoginCommandResponse,
    remember: boolean
  ): Observable<LoginCommandResponse> {
    return of(response).pipe(
        tap(response => {
          console.log('[AuthService] Login successful');
          console.log('[AuthService] Token length:', response.authToken.length);

          // update AppStore
          this.appStore.updateUserInfo({ fullName: response.fullName, email: response.email });
          // persist authToken in localStorage or sessionStorage
          if (remember) {
            localStorage.setItem(authTokenName, response.authToken);
          } else {
            sessionStorage.setItem(authTokenName, response.authToken);
          }

          this.appStore.clearUserMenu();
          // Wait a bit before reconnecting to ensure token is stored
          setTimeout(() => {
            console.log('[AuthService] Attempting SignalR reconnection...');
             this.realtimeService.reconnectAfterLogin();
          }, 500);
        }),
        // Permissions are no longer in the token: load them from the server cache
        switchMap(response => this.permissionService.load().pipe(map(() => response))),
        // Load user menu after successful login
        switchMap(response => {
          return this.userMenuService.getUserMenu().pipe(
            tap(userMenuSource => {
              const localizedMenu = this.userMenuService.localizeMenu(userMenuSource);
              this.appStore.setUserMenu(userMenuSource, localizedMenu);
            }),
            map(() => response),  // Return original login response
            catchError(() => of(response))
          );
        })
    );
  }

  logout(): void {
    this.http.post<void>(`${this.apiUrl}/logout`, {}).subscribe({
      next: () => {
        this.clearLocalAuth();
      },
      error: () => {
        // Even if backend call fails, clear local auth
        this.clearLocalAuth();
      }
    });
  }

  private clearLocalAuth(): void {
    localStorage.removeItem(authTokenName);
    sessionStorage.removeItem(authTokenName);
    this.appStore.clearUserMenu();
    // Clear permissions
    this.permissionService.clearPermissions();
    // Disconnect SignalR
    this.realtimeService.disconnect();
    this.router.navigate(['/login']);
  }

  isAuthenticated(): boolean {
    return !!(localStorage.getItem(authTokenName) || sessionStorage.getItem(authTokenName));
  }

  getToken(): string | null {
    return localStorage.getItem(authTokenName) || sessionStorage.getItem(authTokenName);
  }

  /**
   * Load permissions from the server for the stored token.
   * Call this on app initialization to restore permissions after page refresh;
   * the app initializer waits for it so guards see the loaded permissions.
   */
  initializePermissions(): Observable<void> {
    if (!this.getToken()) {
      return of(undefined);
    }

    // The login flow is not run for a restored session, so connect SignalR here;
    // without it the tab would never receive live pushes such as PermissionsChanged.
    void this.realtimeService.connect();

    return this.permissionService.load().pipe(
      catchError(() => {
        this.permissionService.clearPermissions();
        return of(undefined);
      })
    );
  }
  sendOtpEmail(email: string): Observable<SendOtpEmailCommandResponse> {
    return this.http.post<SendOtpEmailCommandResponse>(`${this.apiUrl}/sendOtpEmail`, { email });
  }

  resetPassword(data: { email: string; otp: string; newPassword: string }): Observable<ResetPasswordCommandResponse> {
    return this.http.post<ResetPasswordCommandResponse>(`${this.apiUrl}/resetPassword`, data);
  }
  
  verifyOtp(data: { email: string; otp: string }): Observable<VerifyOtpCommandResponse> {
    return this.http.post<VerifyOtpCommandResponse>(`${this.apiUrl}/verifyOtp`, data);
  }

  sendOtpToCurrentUser(): Observable<SendOtpToCurrentUserCommandResponse> {
    return this.http.post<SendOtpToCurrentUserCommandResponse>(`${this.apiUrl}/sendOtpToCurrentUser`, {});
  }

  changeCurrentUserPassword(data: { otp: string; newPassword: string }): Observable<ChangeCurrentUserPasswordCommandResponse> {
    return this.http.post<ChangeCurrentUserPasswordCommandResponse>(`${this.apiUrl}/changeCurrentUserPassword`, data);
  }
}
