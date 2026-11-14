import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private http = inject(HttpClient);

  apiBaseUrl!: string;

  async load(): Promise<void> {
    const cfg = await firstValueFrom(
      this.http.get<{ apiBaseUrl: string }>('/app-config.json')
    );

    this.apiBaseUrl = cfg.apiBaseUrl;
  }
}
