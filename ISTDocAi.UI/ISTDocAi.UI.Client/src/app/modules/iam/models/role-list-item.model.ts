// interfaces matching backend commands & queries
export interface RoleGetListRequest {
  Name?: string;
  SortField?: string;
  SortOrder?: string;
  PageNo: number;
  PageRowCount: number;
}

export interface RoleListItem {
  Id: string;
  Name: string;
  NameEn: string;
  NameAr: string;
  IsActive: boolean;
  DisplayOrder: number;
  ConcurrencyStamp: string;
}

export interface RoleAddCommand {
  Name: string;
  NameEn: string;
  NameAr: string;
  IsActive: boolean;
  DisplayOrder: number;
}

export interface RoleUpdateCommand {
  Id: string;
  Name: string;
  NameEn: string;
  NameAr: string;
  IsActive: boolean;
  DisplayOrder: number;
  ConcurrencyStamp?: string;
}

export interface RoleDeleteCommand {
  items: {
    Id: string;
    ConcurrencyStamp?: string;
  }[];
}

export interface RoleGetListQuery {
  Name?: string;
  SortField?: string;
  SortOrder?: string;
  PageNo: number;
  PageRowCount: number;
}

export type GetRoleResponse = RoleListItem;

export interface AddRoleCommandResponse {
  Id: string;
}

export interface UpdateRoleCommandResponse {
  Message: string;
}

export interface DeleteRoleCommandResponse {
  Message: string;
}


