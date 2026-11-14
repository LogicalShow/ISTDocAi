import { Component, effect, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { PasswordPolicyResponse } from '../../../core/models/auth.model';
import { IconComponent } from '../../icons/icon.component';
import { PasswordRuleView, buildPasswordRules, passwordPolicyValidator } from '../../utils/password-policy.utils';

/**
 * "New password" (+ optional "Confirm new password") inputs with eye toggles
 * and the live requirements checklist - Figma "Password fields" (ISTDocAi
 * Dashboard, node 1936:9488). It is presentation-only: its owner supplies the
 * server policy and loading state. The component installs the matching
 * validator on `newPasswordControl`. The confirm field renders only when
 * `confirmPasswordControl` is passed; the owner still decides when to flag a
 * mismatch on submit, this only shows it.
 * Used by add-user, forgot-password, the change-password modal and the
 * admin "Change User Password" modal.
 */
@Component({
  selector: 'app-new-password-fields',
  imports: [ReactiveFormsModule, TranslatePipe, IconComponent],
  templateUrl: './new-password-fields.component.html',
  styleUrl: './new-password-fields.component.css'
})
export class NewPasswordFieldsComponent {
  readonly newPasswordControl = input.required<FormControl<string>>();
  readonly policy = input.required<PasswordPolicyResponse | null>();
  readonly policyLoading = input.required<boolean>();
  /** Omit to render the new-password field alone (admin reset has no confirm). */
  readonly confirmPasswordControl = input<FormControl<string>>();
  /** Prefix for the input ids, so two instances on one page don't collide. */
  readonly idPrefix = input('new-password');

  readonly passwordVisible = signal(false);
  readonly confirmPasswordVisible = signal(false);

  constructor() {
    effect(() => {
      const control = this.newPasswordControl();
      const policy = this.policy();
      control.setValidators(
        policy
          ? [Validators.required, passwordPolicyValidator(policy)]
          : [Validators.required]
      );
      control.updateValueAndValidity({ emitEvent: false });
    });
  }

  rules(): PasswordRuleView[] {
    const policy = this.policy();
    return policy ? buildPasswordRules(policy, this.newPasswordControl().value) : [];
  }

  showMismatch(): boolean {
    const confirm = this.confirmPasswordControl();
    return !!confirm && confirm.touched && this.newPasswordControl().value !== confirm.value;
  }

  serverError(): string | null {
    const error = this.newPasswordControl().errors?.['server'];
    return this.newPasswordControl().touched && typeof error === 'string' ? error : null;
  }

  toggleVisibility(field: 'password' | 'confirm'): void {
    const target = field === 'password' ? this.passwordVisible : this.confirmPasswordVisible;
    target.update(visible => !visible);
  }
}
