import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { ApplicationModuleDto } from '../models/permission-tree.model';
import { ApiHttpService } from '../../../core/services/api-http.service';

export type { ApplicationModuleDto, ModuleDto, PageActionDto, PageDto } from '../models/permission-tree.model';

export interface UserPermissionsDto {
    UserId: string;
    UserName: string;
    Applications: ApplicationModuleDto[];
}

export interface AssignUserPermissionsRequest {
    UserId: string;
    PageActionIds: string[];
    SourceRoleId?: string | null;
}

/** Role permission tree returned by `GetRolePermissions`, used to preload the editor. */
export interface RolePermissionsForImportDto {
    RoleId: string;
    RoleNameEn: string;
    RoleNameAr: string;
    Applications: ApplicationModuleDto[];
}

@Injectable({
    providedIn: 'root'
})
export class UserPermissionsService {
    private http = inject(ApiHttpService);
    private baseUrl = '/api/IAM/UserPermissions';

    getUserPermissions(userId: string): Observable<UserPermissionsDto> {
        return this.http.get<UserPermissionsDto>(`${this.baseUrl}/Get?userId=${userId}`);
    }

    assignUserPermissions(request: AssignUserPermissionsRequest): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/Assign`, request);
    }

    /** Read-only. Fetches the role's permission tree so the editor can preload it locally. */
    getRolePermissionsForImport(roleId: string): Observable<RolePermissionsForImportDto> {
        return this.http.get<RolePermissionsForImportDto>(`${this.baseUrl}/GetRolePermissions?roleId=${roleId}`);
    }
}
