import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiHttpService } from '../../core/services/api-http.service';

export interface LocalizedLookupItem {
  Id: string;
  Code: string | null;
  Name: string;
}

@Injectable({
  providedIn: 'root'
})
export class LookupService {
  private readonly baseUrl = '/api/public/Lookup';
  private http = inject(ApiHttpService);

  getList(): Observable<LocalizedLookupItem[]> {
    return this.http.get<LocalizedLookupItem[]>(`${this.baseUrl}/GetList`);
  }
}
