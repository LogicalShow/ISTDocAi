import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiHttpService } from './api-http.service';

export interface ReportTokenResponse {
  reportToken: string;
  expiresInSeconds: number;
}

/**
 * Issues short-lived tokens for opening server-rendered reports in a new tab.
 * Report tabs are plain browser navigations (no Authorization header, no auth cookie),
 * so the token travels as a POST form field instead.
 */
@Injectable({ providedIn: 'root' })
export class ReportTokenService {
  private http = inject(ApiHttpService);
  private readonly apiUrl = '/api/iam/auth';

  getReportToken(): Observable<ReportTokenResponse> {
    return this.http.post<ReportTokenResponse>(`${this.apiUrl}/reportToken`);
  }
}
