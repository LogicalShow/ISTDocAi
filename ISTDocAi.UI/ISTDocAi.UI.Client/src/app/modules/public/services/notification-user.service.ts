import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpParams } from '@angular/common/http';
import { ApiHttpService } from '../../../core/services/api-http.service';
import { GetListResult } from '../../../shared/models/get-list-result.model';
import {
  NotificationUserUpsertCommand,
  NotificationUserUpsertCommandResponse,
  ServerNotificationUserDetail,
  UserGetListQueryResponse
} from '../models/notification-user.model';

@Injectable({
  providedIn: 'root'
})
export class NotificationUserService {
  private http = inject(ApiHttpService);

  private readonly apiUrl = '/api/public/notificationuser';

  /**
   * Update notification users (add/update/delete)
   * POST: /api/public/NotificationUser/UpdateNotificationUsersAsync
   */
  updateNotificationUsers(command: NotificationUserUpsertCommand): Observable<NotificationUserUpsertCommandResponse> {
    return this.http.post<NotificationUserUpsertCommandResponse>(
      `${this.apiUrl}/UpdateNotificationUsers`,
      command
    );
  }

  getUserList(): Observable<UserGetListQueryResponse[]> {
    return this.http.get<UserGetListQueryResponse[]>(`${this.apiUrl}/GetUserList`);
  }

  /**
   * Get user notifications
   * GET: /api/public/NotificationUser/GetUserNotifications
   */
  getUserNotifications(): Observable<ServerNotificationUserDetail[]> {
    return this.http.get<ServerNotificationUserDetail[]>(`${this.apiUrl}/GetUserNotifications`);
  }

  getUserNotificationsPaginated(
    pageNo = 1,
    pageRowCount = 10,
    unreadOnly = false
  ): Observable<GetListResult<ServerNotificationUserDetail>> {
    const params = new HttpParams()
      .set('pageNo', pageNo.toString())
      .set('pageRowCount', pageRowCount.toString())
      .set('unreadOnly', unreadOnly.toString());
    return this.http.get<GetListResult<ServerNotificationUserDetail>>(
      `${this.apiUrl}/GetUserNotificationsPaginated`, { params }
    );
  }

  addnotificationsToUser(): Observable<void> {
    return this.http.get<void>(
      `${this.apiUrl}/AddUserToAssienedNotificationGroups`
    
    );
  }

}
