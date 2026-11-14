import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, HttpTransportType } from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { ServerNotification, ServerNotificationUserDetail } from '../models/server-notification.model';
import { FeedbackService } from './feedback.service';
import { AppStore } from '../state/app.store';
import { NotificationUserService } from '../../modules/public/services/notification-user.service';

@Injectable({
  providedIn: 'root'
})
export class RealtimeService {
  private feedbackService = inject(FeedbackService);
  private notificationUserService = inject(NotificationUserService);
  private appStore = inject(AppStore);

  private hubConnection: HubConnection;
  private serverNotifications = new BehaviorSubject<ServerNotification[] | null>(null);
  private isConnectionStarted = false;

  constructor() {
   
    this.hubConnection = new HubConnectionBuilder()
      .withUrl('/realtimeHub', {
        withCredentials: true,
        skipNegotiation: false,
        transport: HttpTransportType.WebSockets,
        accessTokenFactory: async () => {
          const token = this.getAccessToken();
          if (!token) {
            console.warn('SignalR: No access token found in storage');
          }
          return token;
        }
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.registerSignalREvents();
    // Don't start connection here - wait for login
  }
private async reconnect(): Promise<void> {
  try {
    await this.hubConnection.stop();
    await this.hubConnection.start();
  } catch (e) {
    console.error('Reconnect failed', e);
  }
}

  private registerSignalREvents(): void {
    this.hubConnection.on('ServerNotifications', (sn: ServerNotification[]) => {
      this.serverNotifications.next(sn);
    });

    this.hubConnection.onreconnecting((error) => {
      console.warn('Reconnecting to SignalR hub...', error);
      // Clear connection ID during reconnection
      this.appStore.updateConnectionId(null);
      // notify UI about reconnection state
      this.feedbackService.warning(
        'feedback.realtime.reconnecting.title',
        'feedback.realtime.reconnecting.message'
      );
    });

    this.hubConnection.onreconnected((connectionId) => {
      console.log('Successfully reconnected to SignalR hub', connectionId);
      // Update connection ID when reconnected
      this.appStore.updateConnectionId(connectionId ?? null);
      this.permissionsChanged$.next();
    });

    // this.hubConnection.onclose((error) => {
    //   console.error('SignalR connection permanently closed after retries', error);
    //   // Clear connection ID when connection is closed
    //   this.appStore.updateConnectionId(null);
    //   // notify UI and let user retry later
    //   this.feedbackService.error('Connection Error', 'Failed to connect to the server. Please try again later.');
    // });

    // Automatic reconnect gives up after its retry schedule (e.g. server restart). While the
    // user is still signed in, keep trying so live pushes such as PermissionsChanged resume.
    this.hubConnection.onclose(() => {
      this.appStore.updateConnectionId(null);
      this.scheduleRestart();
    });

    this.hubConnection.on('ReceiveOldNotifications', (items: ServerNotificationUserDetail[]) => {
  this.oldNotifications$.next(items ?? []);
});

this.hubConnection.on('ReceiveNotification', (n: ServerNotificationUserDetail) => {
  this.newNotification$.next(n);
});

this.hubConnection.on('SubscriptionsChanged', async () => {
  this.subscriptionsChanged$.next();
  await this.reconnect(); // stop/start -> OnConnectedAsync -> ReceiveOldNotifications
 
});
this.hubConnection.on('PermissionsChanged', () => this.permissionsChanged$.next());

  }

  private async startConnection(): Promise<void> {
    if (this.isConnectionStarted) {
      console.log('SignalR connection already started');
      return;
    }
    
    try {
      await this.hubConnection.start();
      this.isConnectionStarted = true;
      console.log('SignalR connection started successfully');
      // Update connection ID when connection is established
      this.appStore.updateConnectionId(this.hubConnection.connectionId);
    } catch (err) {
      console.error('Error starting SignalR connection:', err);
      // Clear connection ID on error
      this.appStore.updateConnectionId(null);
      // Don't show error notification if user is not logged in yet
    }
  }

  public getServerNotifications(): Observable<ServerNotification[] | null> {
    return this.serverNotifications.asObservable();
  }

private oldNotifications$ = new Subject<ServerNotificationUserDetail[]>();
private newNotification$ = new Subject<ServerNotificationUserDetail>();
private subscriptionsChanged$ = new Subject<void>();
private permissionsChanged$ = new Subject<void>();

public onOldNotifications() { return this.oldNotifications$.asObservable(); }
public onNewNotification() { return this.newNotification$.asObservable(); }
public onSubscriptionsChanged() { return this.subscriptionsChanged$.asObservable(); }
public onPermissionsChanged() { return this.permissionsChanged$.asObservable(); }

private startPromise?: Promise<void>;
private stopping = false;

private async safeStart() {
  if (this.hubConnection.state === 'Connected') return;
  if (this.startPromise) return this.startPromise;

  this.startPromise = (async () => {
    try {
      while (this.stopping) await new Promise(r => setTimeout(r, 50));
      await this.hubConnection.start();
      this.appStore.updateConnectionId(this.hubConnection.connectionId);
    } catch {
      this.appStore.updateConnectionId(null);
    } finally {
      this.startPromise = undefined;
    }
  })();

  return this.startPromise;
}

private async safeStop() {
  if (this.hubConnection.state === 'Disconnected') return;
  this.stopping = true;
  try {
    await this.hubConnection.stop();
  } catch (error) {
    console.warn('[RealtimeService] Failed to stop the SignalR connection cleanly.', error);
  } finally {
    this.stopping = false;
  }
}

/**
 * Connect for an already signed-in session (e.g. after a page refresh, when the token is
 * restored from storage and the login flow does not run). No-op if already connected.
 */
public async connect() {
  await this.safeStart();
}

private restartTimer?: ReturnType<typeof setTimeout>;

private isConnected(): boolean {
  return this.hubConnection.state === HubConnectionState.Connected;
}

private scheduleRestart(delayMs = 5000) {
  if (this.restartTimer || this.stopping || !this.getAccessToken()) return;

  this.restartTimer = setTimeout(async () => {
    this.restartTimer = undefined;
    if (!this.getAccessToken() || this.hubConnection.state !== 'Disconnected') return;

    await this.safeStart();
    if (this.isConnected()) {
      // Events pushed while disconnected were missed: resync permissions.
      this.permissionsChanged$.next();
    } else {
      this.scheduleRestart(Math.min(delayMs * 2, 60000));
    }
  }, delayMs);
}

public async reconnectAfterLogin() {
  await this.safeStop();
  await new Promise(r => setTimeout(r, 150));
  await this.safeStart();
}

public async disconnect() {
  clearTimeout(this.restartTimer);
  this.restartTimer = undefined;
  await this.safeStop();
  this.isConnectionStarted = false;
  this.appStore.updateConnectionId(null);
  console.log('SignalR disconnected');
}

  /**
   * Get access token from localStorage/sessionStorage for SignalR authentication
   * Authentication uses the bearer token stored by AuthService.
   */
  private getAccessToken(): string {
    try {
      // Check localStorage first (remember me), then sessionStorage
      const token = localStorage.getItem('authToken') || sessionStorage.getItem('authToken') || '';
      
      if (token) {
        console.log('SignalR: Access token found in storage');
      } else {
        console.warn('SignalR: No access token found in storage');
      }
      
      return token;
    } catch (error) {
      console.error('SignalR: Error reading access token from storage:', error);
      return '';
    }
  }
}
