import { Component, OnDestroy, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { NzModalRef } from 'ng-zorro-antd/modal';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../../core/services/auth.service';
import { FeedbackService } from '../../../../core/services/feedback.service';
import { AppStore } from '../../../../core/state/app.store';
import { PasswordPolicyResponse } from '../../../../core/models/auth.model';
import { ButtonComponent } from '../../../../shared/components/button/button.component';
import { IconComponent } from '../../../../shared/icons/icon.component';
import { NewPasswordFieldsComponent } from '../../../../shared/components/new-password-fields/new-password-fields.component';
import { OtpCodeInputComponent } from '../../../../shared/components/otp-code-input/otp-code-input.component';
import { OtpCountdown } from '../../../../shared/utils/otp-countdown';
import { PasswordPolicyService } from '../../../../core/services/password-policy.service';

type ChangePasswordStep = 'send' | 'code' | 'password';
@Component({
  selector: 'app-change-current-user-password',
  templateUrl: './change-current-user-password.component.html',
  styleUrls: ['./change-current-user-password.component.css'],
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    ButtonComponent,
    IconComponent,
    OtpCodeInputComponent,
    NewPasswordFieldsComponent
  ]
})
export class ChangeCurrentUserPasswordComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly feedbackService = inject(FeedbackService);
  private readonly modal = inject(NzModalRef);
  private readonly appStore = inject(AppStore);
  private readonly passwordPolicyService = inject(PasswordPolicyService);

  private readonly otpInput = viewChild(OtpCodeInputComponent);

  readonly step = signal<ChangePasswordStep>('send');
  readonly userEmail = signal(this.appStore.getState().userInfo?.email ?? '');
  readonly isLoading = signal(false);
  readonly isResending = signal(false);
  readonly countdown = new OtpCountdown(() => this.onOtpExpired());
  readonly passwordPolicy = signal<PasswordPolicyResponse | null>(null);
  readonly isPasswordPolicyLoading = signal(true);

  readonly form = this.fb.nonNullable.group({
    otp: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    newPassword: ['', Validators.required],
    confirmPassword: ['', Validators.required]
  });

  ngOnInit(): void {
    this.passwordPolicyService.policy$
      .pipe(finalize(() => this.isPasswordPolicyLoading.set(false)))
      .subscribe({
        next: policy => this.passwordPolicy.set(policy),
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  sendOtp(): void {
    if (this.isLoading()) {
      return;
    }

    this.isLoading.set(true);
    this.auth.sendOtpToCurrentUser()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.step.set('code');
          this.countdown.start();
        },
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  resendCode(): void {
    if (this.isResending() || this.isLoading()) {
      return;
    }

    this.isResending.set(true);
    this.auth.sendOtpToCurrentUser()
      .pipe(finalize(() => this.isResending.set(false)))
      .subscribe({
        next: () => {
          this.clearOtp();
          this.countdown.start();
          this.feedbackService.success('feedback.auth.otpSent.title', 'feedback.auth.otpSent.message');
        },
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  verifyOtp(): void {
    if (this.isLoading()) {
      return;
    }

    if (this.form.controls.otp.invalid) {
      this.form.controls.otp.markAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.auth.verifyOtp({ email: this.userEmail(), otp: this.form.controls.otp.value })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.countdown.stop();
          this.step.set('password');
        },
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  changePassword(): void {
    if (this.isLoading()) {
      return;
    }

    const { otp, newPassword, confirmPassword } = this.form.getRawValue();
    if (!this.passwordPolicy() || this.form.controls.newPassword.invalid || !confirmPassword || newPassword !== confirmPassword) {
      this.form.controls.newPassword.markAsTouched();
      this.form.controls.confirmPassword.setErrors(
        newPassword !== confirmPassword ? { mismatch: true } : this.form.controls.confirmPassword.errors
      );
      this.form.controls.confirmPassword.markAsTouched();
      return;
    }

    this.form.controls.confirmPassword.setErrors(null);
    this.isLoading.set(true);
    this.auth.changeCurrentUserPassword({ otp, newPassword })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.feedbackService.success('feedback.auth.passwordChanged.title', 'feedback.auth.passwordChanged.message');
          this.modal.close('success');
        },
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  cancel(): void {
    this.modal.close();
  }

  ngOnDestroy(): void {
    this.countdown.stop();
  }

  private clearOtp(): void {
    this.form.controls.otp.reset('');
    this.otpInput()?.focus();
  }

  private onOtpExpired(): void {
    this.clearOtp();
    this.feedbackService.warning('feedback.auth.otpExpired.title', 'feedback.auth.otpExpired.message');
  }
}
