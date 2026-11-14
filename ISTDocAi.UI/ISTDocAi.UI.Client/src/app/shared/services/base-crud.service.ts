import { inject } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { ApiHttpService } from '../../core/services/api-http.service';
import { GetListResult } from '../models/get-list-result.model';

export abstract class BaseCrudService<
  TListItem,
  TQuery,
  TAdd,
  TUpdate,
  TDelete,
  TGetResponse = unknown,
  TAddResponse = unknown,
  TUpdateResponse = unknown,
  TDeleteResponse = unknown,
  TGetAnyResponse = unknown
> {
  protected http = inject(ApiHttpService);
  protected abstract baseUrl: string;
  private addedSource = new Subject<void>();
  added$ = this.addedSource.asObservable();

  notifyAdded() {
    this.addedSource.next();
  }
  getList(query: TQuery): Observable<GetListResult<TListItem>> {
    return this.http.post<GetListResult<TListItem>>(`${this.baseUrl}/getlist`, query);
  }

  getById(id: string | number): Observable<TGetResponse> {
    return this.http.get<TGetResponse>(`${this.baseUrl}/get/${id}`);
  }

  add(command: TAdd, idempotencyKey: string | null): Observable<TAddResponse> {
    const options = {
      headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
    };
    return this.http.post<TAddResponse>(`${this.baseUrl}/add`, command, options);
  }

  update(command: TUpdate): Observable<TUpdateResponse> {
    return this.http.post<TUpdateResponse>(`${this.baseUrl}/update`, command);
  }

  delete(command: TDelete): Observable<TDeleteResponse> {
    return this.http.post<TDeleteResponse>(`${this.baseUrl}/delete`, command);
  }

  getAny(): Observable<TGetAnyResponse> {
    return this.http.get<TGetAnyResponse>(`${this.baseUrl}/GetAny`);
  }
}
