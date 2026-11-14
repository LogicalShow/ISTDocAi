import {
  HttpClient,
  HttpContext,
  HttpHeaders,
  HttpParams
} from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Result } from '../../shared/models/result.model';
import { rethrowApiError, unwrapResult } from '../../shared/utils/api-result.utils';

type ApiParameterValue =
  | string
  | number
  | boolean
  | readonly (string | number | boolean)[];

export interface ApiRequestOptions {
  headers?: HttpHeaders | Record<string, string | string[]>;
  context?: HttpContext;
  params?: HttpParams | Record<string, ApiParameterValue>;
  reportProgress?: boolean;
  withCredentials?: boolean;
}

export interface ApiDeleteOptions extends ApiRequestOptions {
  body?: unknown;
}

@Injectable({ providedIn: 'root' })
export class ApiHttpService {
  private http = inject(HttpClient);

  get<T>(path: string, options?: ApiRequestOptions): Observable<T> {
    return this.http
      .get<Result<T>>(path, options)
      .pipe(unwrapResult());
  }

  post<T>(path: string, body: unknown = {}, options?: ApiRequestOptions): Observable<T> {
    return this.http
      .post<Result<T>>(path, body, options)
      .pipe(unwrapResult());
  }

  put<T>(path: string, body: unknown = {}, options?: ApiRequestOptions): Observable<T> {
    return this.http
      .put<Result<T>>(path, body, options)
      .pipe(unwrapResult());
  }

  delete<T>(path: string, options?: ApiDeleteOptions): Observable<T> {
    return this.http
      .delete<Result<T>>(path, options)
      .pipe(unwrapResult());
  }

  getBlob(path: string, options?: ApiRequestOptions): Observable<Blob> {
    return this.http
      .get(path, { ...options, responseType: 'blob' })
      .pipe(catchError(rethrowApiError));
  }

  getBlobWithFilename(
    path: string,
    options?: ApiRequestOptions
  ): Observable<{ blob: Blob; filename: string }> {
    return this.http
      .get(path, { ...options, responseType: 'blob', observe: 'response' })
      .pipe(
        map(response => ({
          blob: response.body as Blob,
          filename: this.getFilename(response.headers.get('Content-Disposition'))
        })),
        catchError(rethrowApiError)
      );
  }

  private getFilename(contentDisposition: string | null): string {
    if (!contentDisposition) {
      return 'download';
    }

    const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(contentDisposition);
    return match?.[1]?.replace(/['"]/g, '') || 'download';
  }
}
