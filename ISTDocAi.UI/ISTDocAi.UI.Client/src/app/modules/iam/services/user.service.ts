import { Injectable } from '@angular/core';
import {
    UserAddCommand,
    AddUserCommandResponse,
    UserChangePasswordCommand,
    ChangeUserPasswordCommandResponse,
    UserDeleteCommand,
    DeleteUserCommandResponse,
    GetUserResponse,
    UserGetListQuery,
    UserListItem,
    UserUpdateCommand,
    UpdateUserCommandResponse
} from '../models/user-list-item.model';
import { BaseCrudService } from '../../../shared/services/base-crud.service';

@Injectable({
    providedIn: 'root'
})
export class UserService extends BaseCrudService<
    UserListItem,
    UserGetListQuery,
    UserAddCommand,
    UserUpdateCommand,
    UserDeleteCommand,
    GetUserResponse,
    AddUserCommandResponse,
    UpdateUserCommandResponse,
    DeleteUserCommandResponse
> {
    protected override baseUrl = '/api/IAM/User';

    changePassword(command: UserChangePasswordCommand) {
        return this.http.post<ChangeUserPasswordCommandResponse>(`${this.baseUrl}/ChangePassword`, command);
    }
}
