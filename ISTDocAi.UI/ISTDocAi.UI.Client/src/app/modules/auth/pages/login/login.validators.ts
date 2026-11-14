import { ValidatorFn, Validators } from '@angular/forms';

export class LoginValidators {
  static username: ValidatorFn[] = [Validators.required];
  static password: ValidatorFn[] = [Validators.required, Validators.minLength(6)];

  static getErrorMessage(controlName: string, errorKey: string): string {
    const errorMessages: Record<string, Record<string, string>> = {
      username: {
        required: 'auth.login.userName.required',
        serverAuth: ''
      },
      password: {
        required: 'auth.login.password.required',
        minlength: 'auth.login.password.minLength',
        serverAuth: ''
      }
    };

    const message = errorMessages[controlName]?.[errorKey];
    return message !== undefined ? message : 'validation.common.invalid';
  }
}
