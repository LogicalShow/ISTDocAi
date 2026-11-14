import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpParams } from '@angular/common/http';
import { ApiHttpService } from '../../../core/services/api-http.service';
import { GetListResult } from '../../../shared/models/get-list-result.model';
import {
  NotificationGetListQuery,
  NotificationListItem,
  NotificationDetails,
  NotificationLookupItem
} from '../models/notification.model';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private http = inject(ApiHttpService);

  private readonly apiUrl = '/api/public/notification';

  /**
   * Get notification by ID with details
   * GET: /api/public/Notification/Get/{Id}
   */
  getById(id: string): Observable<NotificationDetails> {
    return this.http.get<NotificationDetails>(`${this.apiUrl}/get/${id}`);
  }

  /**
   * Get list of notifications with filtering and pagination
   * POST: /api/public/Notification/GetList
   */
  getList(query: NotificationGetListQuery): Observable<GetListResult<NotificationListItem>> {
    return this.http.post<GetListResult<NotificationListItem>>(`${this.apiUrl}/getlist`, query);
  }

  /**
   * Search notifications for select dropdown (excludes existing notification users)
   * GET: /api/public/Notification/SearchForSelect?term=xxx&pageSize=20
   */
  searchForSelect(term: string, pageSize = 20): Observable<NotificationLookupItem[]> {
    let params = new HttpParams()
      .set('pageSize', pageSize.toString());
    
    const trimmed = (term ?? '').trim();
    if (trimmed) {
      params = params.set('term', trimmed);
    }

    return this.http.get<NotificationLookupItem[]>(
      `${this.apiUrl}/SearchForSelect`,
      { params }
    );
  }
}
