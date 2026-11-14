import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { inject } from '@angular/core';
import type { ApplicationModuleDto } from '../models/permission-tree.model';
import { ApiHttpService } from '../../../core/services/api-http.service';

export type { ApplicationModuleDto, ModuleDto, PageActionDto, PageDto } from '../models/permission-tree.model';

export interface RolePermissionsDto {
  RoleId: string;
  RoleNameEn: string;
  RoleNameAr: string;
  Applications: ApplicationModuleDto[];
}

export interface AssignRolePermissionsRequest {
  RoleId: string;
  PageActionIds: string[];
}

@Injectable({
  providedIn: 'root'
})
export class RolePermissionsService {
  private http = inject(ApiHttpService);
  private baseUrl = '/api/IAM/RolePermissions';

  getRolePermissions(roleId: string): Observable<RolePermissionsDto> {
    return this.http.get<RolePermissionsDto>(`${this.baseUrl}/Get?roleId=${roleId}`);
  }

  assignRolePermissions(request: AssignRolePermissionsRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/Assign`, request);
  }
}

