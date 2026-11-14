import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiHttpService } from '../../../core/services/api-http.service';

// Command interfaces
export interface MarkAsReadCommand {
  NotificationId: string;
  CorrelationId: string;
}

export interface MarkAsReadCommandResponse {
  Message: string;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationUserDetailService {
  private http = inject(ApiHttpService);

  private readonly apiUrl = '/api/public/NotificationUserDetail';

  /**
   * Mark notification as read
   * POST: /api/public/NotificationUserDetail/MarkAsRead
   */
  markAsRead(command: MarkAsReadCommand): Observable<MarkAsReadCommandResponse> {
    return this.http.post<MarkAsReadCommandResponse>(`${this.apiUrl}/MarkAsRead`, command);
  }
}
