export interface UserGetListRequest {
  UserName?: string;
  Email?: string;
  SortField?: string;
  SortOrder?: string;
  PageNo: number;
  PageRowCount: number;
}

export interface UserListItem {
  Id: string;
  UserName: string;
  Email: string;
  FullName: string;
  IsActive: boolean;
  IsAdministrator: boolean;
  RoleId?: string;
  ConcurrencyStamp: string;
}

export interface UserAddCommand {
  UserName: string;
  Email: string;
  FullName: string;
  Password: string;
  IsActive: boolean;
  IsAdministrator: boolean;
  RoleId?: string;
}

export interface UserUpdateCommand {
  Id: string;
  UserName?: string;
  Email: string;
  FullName: string;
  IsActive: boolean;
  IsAdministrator: boolean;
  RoleId?: string;
  ConcurrencyStamp?: string;
}

export interface UserDeleteCommand {
  items: {
    Id: string;
    ConcurrencyStamp?: string;
  }[];
}

export interface UserChangePasswordCommand {
  Id: string;
  NewPassword: string;
  ConcurrencyStamp?: string;
}

export interface GetUserResponse {
  Id: string;
  UserName: string;
  Email: string | null;
  FullName: string;
  IsActive: boolean;
  IsAdministrator: boolean;
  RoleId: string | null;
  ConcurrencyStamp: string;
}

export interface AddUserCommandResponse {
  Id: string;
}

export interface UpdateUserCommandResponse {
  Message: string;
}

export interface DeleteUserCommandResponse {
  Message: string;
}

export interface ChangeUserPasswordCommandResponse {
  Message: string;
}

export interface UserGetListQuery {
  UserName?: string;
  Email?: string;
  SortField?: string;
  SortOrder?: string;
  PageNo: number;
  PageRowCount: number;
}
