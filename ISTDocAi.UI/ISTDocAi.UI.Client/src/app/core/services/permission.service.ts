import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { ApiHttpService } from './api-http.service';


// Action names and bit positions matching backend ActionDataEnum.
const ACTIONS_BY_BIT: Record<number, string> = {
  0: 'VIEW',
  1: 'ADD',
  2: 'EDIT',
  3: 'DELETE',
  4: 'PRINT'
};

interface PermissionsResponse {
  /** Compressed permissions: { "MD.BP.AGT": 7, ... } */
  permissions: Record<string, number>;
  version: number;
}

/**
 * Holds the signed-in user's permissions.
 *
 * Permissions are no longer carried in the JWT: they are read from the server-side
 * permission cache via GET /api/iam/auth/permissions and kept in a signal, so anything
 * that reads them inside a template, computed() or effect() re-evaluates automatically
 * when PermissionSyncService reloads them after a live permission change.
 */
@Injectable({
  providedIn: 'root'
})
export class PermissionService {
  private http = inject(ApiHttpService);
  private readonly apiUrl = '/api/iam/auth';

  private readonly permissions = signal<ReadonlySet<string>>(new Set());
  readonly loaded = signal(false);
  readonly version = signal(0);

  /**
   * Load the current user's permissions from the server.
   */
  load(): Observable<void> {
    return this.http.get<PermissionsResponse>(`${this.apiUrl}/permissions`).pipe(
      tap(response => {
        this.permissions.set(new Set(this.decompressPermissions(response.permissions ?? {})));
        this.version.set(response.version);
        this.loaded.set(true);
      }),
      map(() => undefined)
    );
  }

  /**
   * Decompress permissions from hierarchical bitflag format
   * Converts { "MD.BP.AGT": 7 } to ["MD.BP.AGT.VIEW", "MD.BP.AGT.ADD", "MD.BP.AGT.EDIT"]
   */
  private decompressPermissions(compressed: Record<string, number>): string[] {
    const permissions: string[] = [];

    for (const [pageId, actionFlags] of Object.entries(compressed)) {
      Object.entries(ACTIONS_BY_BIT).forEach(([bitIndex, action]) => {
        const actionBit = 1 << Number(bitIndex);
        if ((actionFlags & actionBit) !== 0) {
          permissions.push(`${pageId}.${action}`);
        }
      });
    }

    return permissions;
  }

  /**
   * Check if user has a specific permission
   * @param permissionKey Permission key in format {PageId}.{ActionId} (e.g., 'MD.BP.AGT.VIEW')
   */
  hasPermission(permissionKey: string): boolean {
    return this.permissions().has(permissionKey);
  }

  /**
   * Check if user has any of the specified permissions
   */
  hasAnyPermission(permissionKeys: string[]): boolean {
    const permissions = this.permissions();
    return permissionKeys.some(key => permissions.has(key));
  }

  /**
   * Check if user has all of the specified permissions
   */
  hasAllPermissions(permissionKeys: string[]): boolean {
    const permissions = this.permissions();
    return permissionKeys.every(key => permissions.has(key));
  }

  /**
   * Get all user permissions
   */
  getAllPermissions(): string[] {
    return Array.from(this.permissions());
  }

  /**
   * Clear all permissions (useful for logout)
   */
  clearPermissions(): void {
    this.permissions.set(new Set());
    this.loaded.set(false);
    this.version.set(0);
  }

  /**
   * Check if permissions are loaded
   */
  hasLoadedPermissions(): boolean {
    return this.loaded();
  }
}
