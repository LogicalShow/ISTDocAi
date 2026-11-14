import { Injectable, inject } from '@angular/core';
import { shareReplay } from 'rxjs';
import { PasswordPolicyResponse } from '../models/auth.model';
import { ApiHttpService } from './api-http.service';

/**
 * Loads the server's Identity password policy once and shares the result with
 * every password workflow. Failed requests are not cached, so opening a
 * workflow again can retry after a transient failure.
 */
@Injectable({ providedIn: 'root' })
export class PasswordPolicyService {
  private readonly http = inject(ApiHttpService);

  readonly policy$ = this.http
    .get<PasswordPolicyResponse>('/api/iam/auth/passwordPolicy')
    .pipe(shareReplay({ bufferSize: 1, refCount: false }));
}
