import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { PasswordPolicyResponse } from '../../core/models/auth.model';

/** One server password rule, evaluated against the password being typed. */
export interface PasswordRuleView {
  key: string;
  /** i18n key under `auth.passwordRules`. */
  translationKey: string;
  params?: Record<string, number>;
  isMet: boolean;
}

/**
 * Must match the server's Identity password options (exposed through the
 * passwordPolicy endpoint), so forgot-password and change-password show the
 * same checklist and reject the same values the server would.
 */
export function buildPasswordRules(policy: PasswordPolicyResponse, password: string): PasswordRuleView[] {
  const rules: PasswordRuleView[] = [
    {
      key: 'length',
      translationKey: 'auth.passwordRules.minimum',
      params: { count: policy.RequiredLength },
      isMet: password.length >= policy.RequiredLength
    }
  ];

  // A single unique character is satisfied by any non-empty password, so the rule is only worth showing above 1.
  if (policy.RequiredUniqueChars > 1) {
    rules.push({
      key: 'unique',
      translationKey: 'auth.passwordRules.unique',
      params: { count: policy.RequiredUniqueChars },
      isMet: new Set(password).size >= policy.RequiredUniqueChars
    });
  }

  if (policy.RequireUppercase) {
    rules.push({
      key: 'uppercase',
      translationKey: 'auth.passwordRules.uppercase',
      isMet: /\p{Lu}/u.test(password)
    });
  }

  if (policy.RequireLowercase) {
    rules.push({
      key: 'lowercase',
      translationKey: 'auth.passwordRules.lowercase',
      isMet: /\p{Ll}/u.test(password)
    });
  }

  if (policy.RequireDigit) {
    rules.push({
      key: 'digit',
      translationKey: 'auth.passwordRules.digit',
      isMet: /\p{Nd}/u.test(password)
    });
  }

  if (policy.RequireNonAlphanumeric) {
    rules.push({
      key: 'nonAlphanumeric',
      translationKey: 'auth.passwordRules.nonAlphanumeric',
      isMet: /[^\p{L}\p{N}]/u.test(password)
    });
  }

  return rules;
}

/** Fails with `{ passwordPolicy: true }` until every rule in the policy is met. */
export function passwordPolicyValidator(policy: PasswordPolicyResponse): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const password = control.value ?? '';
    return buildPasswordRules(policy, password).every(rule => rule.isMet) ? null : { passwordPolicy: true };
  };
}
