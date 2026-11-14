import { Validators } from '@angular/forms';

export class RoleValidators {
  static nameEn = [
    Validators.required,
    Validators.maxLength(256)
  ];

  static getErrorMessage(controlName: string, errorKey: string): string {
    const errorMessages: Record<string, Record<string, string>> = {
      nameEn: {
        required: 'validation.role.nameEn.required',
        maxlength: 'validation.role.nameEn.maxLength'
      }
    };

    return errorMessages[controlName]?.[errorKey] || 'validation.common.invalid';
  }
}
