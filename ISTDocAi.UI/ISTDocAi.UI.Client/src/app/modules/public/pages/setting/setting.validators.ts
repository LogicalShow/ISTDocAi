import { Validators } from '@angular/forms';

export class SettingValidators {
  static companyNameEn = [
    Validators.required,
    Validators.maxLength(200)
  ];

  static companyNameAr = [
    Validators.maxLength(200)
  ];

  static countryId = [];

  static companyAddress = [
    Validators.required,
    Validators.maxLength(500)
  ];

  static companyPhoneOne = [
    Validators.required,
    Validators.maxLength(50)
  ];

  static companyPhoneTwo = [
    Validators.maxLength(50)
  ];

  static companyEmail = [
    Validators.email,
    Validators.maxLength(100)
  ];

  static companyWebsite = [
    Validators.maxLength(200)
  ];

  static companyFax = [
    Validators.maxLength(50)
  ];

  static defaultCurrencyId = [
    Validators.required
  ];

  static isEmailEnabled = [];

  static emailHost = [
    Validators.required,
    Validators.maxLength(255)
  ];

  static emailPort = [
    Validators.required,
    Validators.min(1),
    Validators.max(65535)
  ];

  static emailUsername = [
    Validators.required,
    Validators.email,
    Validators.maxLength(255)
  ];

  static emailPassword = [
    Validators.required,
    Validators.minLength(6),
    Validators.maxLength(255)
  ];

  static senderEmail = [
    Validators.required,
    Validators.email,
    Validators.maxLength(255)
  ];

  static emailEnableSSL = [];

  static getErrorMessage(controlName: string, errorKey: string): string {
    const errorMessages: Record<string, Record<string, string>> = {
      companyNameEn: {
        required: 'validation.setting.companyNameEn.required',
        maxlength: 'validation.setting.companyNameEn.maxLength'
      },
      companyNameAr: {
        maxlength: 'validation.setting.companyNameAr.maxLength'
      },
      companyAddress: {
        required: 'validation.setting.companyAddress.required',
        maxlength: 'validation.setting.companyAddress.maxLength'
      },
      companyPhoneOne: {
        required: 'validation.setting.companyPhoneOne.required',
        maxlength: 'validation.setting.companyPhoneOne.maxLength'
      },
      companyPhoneTwo: {
        maxlength: 'validation.setting.companyPhoneTwo.maxLength'
      },
      companyEmail: {
        email: 'validation.setting.companyEmail.email',
        maxlength: 'validation.setting.companyEmail.maxLength'
      },
      companyWebsite: {
        maxlength: 'validation.setting.companyWebsite.maxLength'
      },
      companyFax: {
        maxlength: 'validation.setting.companyFax.maxLength'
      },
      defaultCurrencyId: {
        required: 'validation.setting.defaultCurrencyId.required'
      },
      emailHost: {
        required: 'validation.setting.emailHost.required',
        maxlength: 'validation.setting.emailHost.maxLength'
      },
      emailPort: {
        required: 'validation.setting.emailPort.required',
        min: 'validation.setting.emailPort.min',
        max: 'validation.setting.emailPort.max'
      },
      emailUsername: {
        required: 'validation.setting.emailUsername.required',
        email: 'validation.setting.emailUsername.email',
        maxlength: 'validation.setting.emailUsername.maxLength'
      },
      emailPassword: {
        required: 'validation.setting.emailPassword.required',
        minlength: 'validation.setting.emailPassword.minLength',
        maxlength: 'validation.setting.emailPassword.maxLength'
      },
      senderEmail: {
        required: 'validation.setting.senderEmail.required',
        email: 'validation.setting.senderEmail.email',
        maxlength: 'validation.setting.senderEmail.maxLength'
      }
    };

    return errorMessages[controlName]?.[errorKey] || 'validation.common.invalid';
  }
}
