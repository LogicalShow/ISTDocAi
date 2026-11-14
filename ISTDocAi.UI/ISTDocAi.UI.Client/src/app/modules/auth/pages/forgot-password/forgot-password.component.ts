import { Component, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../../core/services/auth.service';
import { FeedbackService } from '../../../../core/services/feedback.service';
import { TranslationService } from '../../../../core/services/translation.service';
import { ButtonComponent } from '../../../../shared/components/button/button.component';
import { LanguageSwitcherComponent } from '../../../../shared/components/language-switcher/language-switcher.component';
import { ThemeSwitcherComponent } from '../../../../shared/components/theme-switcher/theme-switcher.component';
import { NewPasswordFieldsComponent } from '../../../../shared/components/new-password-fields/new-password-fields.component';
import { OtpCodeInputComponent } from '../../../../shared/components/otp-code-input/otp-code-input.component';
import { IconComponent } from '../../../../shared/icons/icon.component';
import { IconName } from '../../../../shared/icons/app-icons';
import { PasswordPolicyResponse } from '../../../../core/models/auth.model';
import { OtpCountdown } from '../../../../shared/utils/otp-countdown';
import { PasswordPolicyService } from '../../../../core/services/password-policy.service';
import { ThemeService } from '../../../../core/services/theme.service';

type ForgotPasswordStep = 'email' | 'code' | 'password' | 'success';

@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.scss'],
  imports: [
    ReactiveFormsModule,
    RouterModule,
    ButtonComponent,
    LanguageSwitcherComponent,
    ThemeSwitcherComponent,
    IconComponent,
    OtpCodeInputComponent,
    NewPasswordFieldsComponent,
    TranslatePipe
  ]
})
export class ForgotPasswordComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly feedbackService = inject(FeedbackService);
  private readonly router = inject(Router);
  private readonly translationService = inject(TranslationService);
  private readonly passwordPolicyService = inject(PasswordPolicyService);
  private readonly themeService = inject(ThemeService);

  private readonly otpInput = viewChild(OtpCodeInputComponent);

  readonly step = signal<ForgotPasswordStep>('email');
  readonly isLoading = signal(false);
  readonly isResending = signal(false);
  readonly countdown = new OtpCountdown(() => this.onOtpExpired());
  readonly passwordPolicy = signal<PasswordPolicyResponse | null>(null);
  readonly isPasswordPolicyLoading = signal(true);
  /**
   * The default logo's wordmark is dark gray and disappears against the dark
   * backdrop. Swapping the src (rather than toggling two <img> elements with
   * CSS) keeps this to a single request instead of downloading both variants.
   */
  readonly logoSrc = computed(() =>
    this.themeService.theme() === 'dark' ? 'assets/logos/logo_ist2_dark.png' : 'assets/logos/logo_ist2.png'
  );

  /** Points toward reading-start: the same visual side in both directions. */
  readonly backIcon = computed<IconName>(() =>
    this.translationService.culture() === 'ar' ? 'arrow-right-light' : 'arrow-left-light'
  );

  readonly emailForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]]
  });

  readonly resetForm = this.fb.nonNullable.group({
    email: [''],
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

  submitEmail(): void {
    if (this.isLoading()) {
      return;
    }

    if (this.emailForm.invalid) {
      this.emailForm.markAllAsTouched();
      return;
    }

    const email = this.emailForm.controls.email.value.trim();
    this.isLoading.set(true);
    this.auth.sendOtpEmail(email)
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.resetForm.controls.email.setValue(email);
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
    this.auth.sendOtpEmail(this.resetForm.controls.email.value)
      .pipe(finalize(() => this.isResending.set(false)))
      .subscribe({
        next: () => {
          this.clearOtp();
          this.countdown.start();
          this.feedbackService.success(
            'feedback.auth.resetCodeSent.title',
            'feedback.auth.resetCodeSent.message'
          );
        },
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  verifyOtp(): void {
    if (this.isLoading()) {
      return;
    }

    if (this.resetForm.controls.otp.invalid) {
      this.resetForm.controls.otp.markAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.auth.verifyOtp({
      email: this.resetForm.controls.email.value,
      otp: this.resetForm.controls.otp.value
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.countdown.stop();
          this.step.set('password');
        },
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  resetPassword(): void {
    if (this.isLoading()) {
      return;
    }

    const { newPassword, confirmPassword } = this.resetForm.getRawValue();
    if (!this.passwordPolicy() || this.resetForm.controls.newPassword.invalid || !confirmPassword || newPassword !== confirmPassword) {
      this.resetForm.controls.newPassword.markAsTouched();
      this.resetForm.controls.confirmPassword.setErrors(
        newPassword !== confirmPassword ? { mismatch: true } : this.resetForm.controls.confirmPassword.errors
      );
      this.resetForm.controls.confirmPassword.markAsTouched();
      return;
    }

    this.resetForm.controls.confirmPassword.setErrors(null);
    this.isLoading.set(true);
    this.auth.resetPassword({
      email: this.resetForm.controls.email.value,
      otp: this.resetForm.controls.otp.value,
      newPassword
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => this.step.set('success'),
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  backToSignIn(): void {
    void this.router.navigate(['/login']);
  }

  ngOnDestroy(): void {
    this.countdown.stop();
  }

  private clearOtp(): void {
    this.resetForm.controls.otp.reset('');
    this.otpInput()?.focus();
  }

  private onOtpExpired(): void {
    this.clearOtp();
    this.feedbackService.warning(
      'feedback.auth.otpExpired.title',
      'feedback.auth.otpExpired.message'
    );
  }
}
