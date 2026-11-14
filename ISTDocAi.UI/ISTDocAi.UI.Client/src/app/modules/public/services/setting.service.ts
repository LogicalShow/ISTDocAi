import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiHttpService } from '../../../core/services/api-http.service';
import { SettingResponse, UpdateSettingResponse } from '../models/setting.model';

@Injectable({
  providedIn: 'root'
})
export class SettingService {
  private http = inject(ApiHttpService);

  private readonly apiUrl = '/api/public/setting';

  /**
   * Get the current setting
   */
  get(): Observable<SettingResponse> {
    return this.http.get<SettingResponse>(`${this.apiUrl}/get`);
  }

  /**
   * Update setting with files (logo, report header, report footer)
   * Uses FormData for multipart/form-data submission
   */
  update(formData: FormData): Observable<UpdateSettingResponse> {
    return this.http.post<UpdateSettingResponse>(`${this.apiUrl}/update`, formData);
  }

  /**
   * Get image by type
   * @param imageType 1=Logo, 2=ReportHeader, 3=ReportFooter
   * @returns Observable of Blob containing the image
   */
  getImage(imageType: number): Observable<Blob> {
    return this.http.getBlob(`${this.apiUrl}/getImage?ImageType=${imageType}`);
  }

  /**
   * Download image by type with filename
   * @param imageType 1=Logo, 2=ReportHeader, 3=ReportFooter
   * @returns Observable with blob and filename
   */
  downloadImage(imageType: number): Observable<{ blob: Blob; filename: string }> {
    return this.http.getBlobWithFilename(`${this.apiUrl}/getImage?ImageType=${imageType}`);
  }
}
