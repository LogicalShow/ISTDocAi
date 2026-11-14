import { Injectable } from '@angular/core';
import { 
  RoleAddCommand, 
  AddRoleCommandResponse,
  RoleDeleteCommand, 
  DeleteRoleCommandResponse,
  GetRoleResponse,
  RoleGetListQuery, 
  RoleListItem, 
  RoleUpdateCommand,
  UpdateRoleCommandResponse
} from '../models/role-list-item.model';
import { BaseCrudService } from '../../../shared/services/base-crud.service';

@Injectable({
  providedIn: 'root'
})
export class RoleService extends BaseCrudService<
   RoleListItem,
   RoleGetListQuery,
   RoleAddCommand,
   RoleUpdateCommand,
   RoleDeleteCommand,
   GetRoleResponse,
   AddRoleCommandResponse,
   UpdateRoleCommandResponse,
   DeleteRoleCommandResponse
  > {
  protected override baseUrl = '/api/IAM/Role';
}




