import { Validators } from '@angular/forms';

export class UserValidators {
  static userName = [
    Validators.required,
    Validators.maxLength(256)
  ];

  static fullName = [
    Validators.required,
    Validators.maxLength(100)
  ];

  static email = [
    Validators.required,
    Validators.email,
    Validators.maxLength(256)
  ];

  static roleId = [
    Validators.required
  ];

  static getErrorMessage(controlName: string, errorKey: string): string {
    const errorMessages: Record<string, Record<string, string>> = {
      userName: {
        required: 'validation.user.userName.required',
        maxlength: 'validation.user.userName.maxLength'
      },
      fullName: {
        required: 'validation.user.fullName.required',
        maxlength: 'validation.user.fullName.maxLength'
      },
      email: {
        required: 'validation.user.email.required',
        email: 'validation.user.email.email',
        maxlength: 'validation.user.email.maxLength'
      },
      roleId: {
        required: 'validation.user.roleId.required'
      }
    };

    return errorMessages[controlName]?.[errorKey] || 'validation.common.invalid';
  }
}
