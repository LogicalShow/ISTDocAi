import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiHttpService } from '../../core/services/api-http.service';
import {
  AttachmentAddRequest,
  AttachmentUpdateRequest,
  AttachmentDeleteRequest,
  AttachmentResponse,
  AttachmentAddResponse,
  AttachmentUpdateResponse,
  AttachmentDeleteResponse
} from '../models/attachment.model';

@Injectable({
  providedIn: 'root'
})
export class AttachmentService {
  private baseUrl = '/api/public/attachment';
  private http = inject(ApiHttpService);

  add(request: AttachmentAddRequest): Observable<AttachmentAddResponse> {
    const formData = new FormData();
    formData.append('OriginalFileName', request.originalFileName);
    if (request.note) {
      formData.append('Note', request.note);
    }
    formData.append('ReferenceType', request.referenceType);
    formData.append('ReferenceId', request.referenceId);
    formData.append('ReferenceSerial', request.referenceSerial);
    formData.append('File', request.file);

    return this.http.post<AttachmentAddResponse>(`${this.baseUrl}/add`, formData);
  }

  update(request: AttachmentUpdateRequest): Observable<AttachmentUpdateResponse> {
    const formData = new FormData();
    formData.append('Id', request.id);
    formData.append('RowVersion', request.rowVersion);
    formData.append('OriginalFileName', request.originalFileName);
    if (request.note) {
      formData.append('Note', request.note);
    }
    formData.append('ReferenceType', request.referenceType);
    formData.append('ReferenceId', request.referenceId);
    formData.append('ReferenceSerial', request.referenceSerial);
    
    if (request.file) {
      formData.append('File', request.file);
    }

    return this.http.put<AttachmentUpdateResponse>(`${this.baseUrl}/update`, formData);
  }

  delete(request: AttachmentDeleteRequest): Observable<AttachmentDeleteResponse> {
    return this.http.delete<AttachmentDeleteResponse>(`${this.baseUrl}/delete`, {
      body: request
    });
  }

  getById(id: string): Observable<AttachmentResponse> {
    return this.http.get<AttachmentResponse>(`${this.baseUrl}/getbyid/${id}`);
  }

  getList(referenceType: string, referenceId: string): Observable<AttachmentResponse[]> {
    return this.http.get<AttachmentResponse[]>(
      `${this.baseUrl}/getlist`,
      {
        params: {
          referenceType,
          referenceId
        }
      }
    );
  }

  getFile(id: string, download = false): Observable<Blob> {
    return this.http.getBlob(`${this.baseUrl}/file/${id}`, {
      params: { download: download.toString() },
    });
  }
}
