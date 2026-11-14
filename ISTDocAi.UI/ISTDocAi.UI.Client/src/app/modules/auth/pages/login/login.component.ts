
import { Component, OnInit, inject, signal, DestroyRef, Injector, Signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../../../core/services/auth.service';
import { FeedbackService } from '../../../../core/services/feedback.service';
import { IconComponent } from '../../../../shared/icons/icon.component';
import { ButtonComponent } from '../../../../shared/components/button/button.component';
import { LanguageSwitcherComponent } from '../../../../shared/components/language-switcher/language-switcher.component';
import { ThemeSwitcherComponent } from '../../../../shared/components/theme-switcher/theme-switcher.component';
import { LoginValidators } from './login.validators';
import { AuthorizedRouteService } from '../../../../core/services/authorized-route.service';
import { formChanges } from '../../../../shared/utils/form-signal-utils';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
  imports: [
    ReactiveFormsModule,
    NzFormModule,
    NzInputModule,
    NzCheckboxModule,
    RouterModule,
    IconComponent,
    ButtonComponent,
    LanguageSwitcherComponent,
    ThemeSwitcherComponent,
    TranslatePipe
]
})
export class LoginComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private feedbackService = inject(FeedbackService);
  private translate = inject(TranslateService);
  private authorizedRouteService = inject(AuthorizedRouteService);
  private destroyRef = inject(DestroyRef);
  private injector = inject(Injector);

  loginForm!: FormGroup;
  private formState?: Signal<unknown>;
  readonly isLoading = signal(false);
  readonly passwordVisible = signal(false);
  readonly loginErrorMessage = signal<string | null>(null);

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      this.navigateToFirstAccessibleRoute();
    }

    this.loginForm = this.fb.group({
      username: ['', LoginValidators.username],
      password: ['', LoginValidators.password],
      remember: [false]
    });

    this.formState = formChanges(this.loginForm, this.injector);

    this.loginForm.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.loginErrorMessage()) {
        this.loginErrorMessage.set(null);
        this.loginForm.get('username')?.updateValueAndValidity({ onlySelf: true, emitEvent: false });
        this.loginForm.get('password')?.updateValueAndValidity({ onlySelf: true, emitEvent: false });
      }
    });
  }

  togglePasswordVisibility(): void {
    this.passwordVisible.update(visible => !visible);
  }

  login(): void {
    if (this.isLoading()) {
      return;
    }

    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.loginErrorMessage.set(null);
    const { username, password, remember } = this.loginForm.value;
    this.authService.login(username, password, !!remember)
      .subscribe({
        next: () => {
          this.loginForm.get('password')?.reset('');
          void this.navigateToFirstAccessibleRoute()
            .finally(() => this.isLoading.set(false));
        },
        error: (error) => {
          this.isLoading.set(false);
          if (error.status === 401) {
            this.loginErrorMessage.set(this.translate.instant('auth.login.incorrectCredentials'));
            this.loginForm.get('username')?.setErrors({ serverAuth: true });
            this.loginForm.get('password')?.setErrors({ serverAuth: true });
            return;
          }

          this.feedbackService.handleApiError(error);
        }
      });
  }

  private navigateToFirstAccessibleRoute(): Promise<boolean> {
    const route = this.authorizedRouteService.getFirstAccessibleRoute() ?? '/';
    return this.router.navigateByUrl(route);
  }

  getErrorMessage(controlName: string): string {
    this.formState?.();
    const control = this.loginForm.get(controlName);
    if (control?.invalid && control?.touched) {
      const errors = control.errors || {};
      const errorKeys = Object.keys(errors);

      if (errorKeys.length > 0) {
        return LoginValidators.getErrorMessage(controlName, errorKeys[0]);
      }
    }
    return '';
  }

}
