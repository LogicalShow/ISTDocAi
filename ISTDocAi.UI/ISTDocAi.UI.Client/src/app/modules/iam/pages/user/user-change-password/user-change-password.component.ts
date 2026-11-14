import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, take } from 'rxjs';
import { NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { TranslatePipe } from '@ngx-translate/core';
import { FeedbackService } from '../../../../../core/services/feedback.service';
import { HasUnsavedChanges } from '../../../../../core/models/has-unsaved-changes.model';
import { PasswordPolicyResponse } from '../../../../../core/models/auth.model';
import { ButtonComponent } from '../../../../../shared/components/button/button.component';
import { NewPasswordFieldsComponent } from '../../../../../shared/components/new-password-fields/new-password-fields.component';
import { IconComponent } from '../../../../../shared/icons/icon.component';
import { UserListItem } from '../../../models/user-list-item.model';
import { UserService } from '../../../services/user.service';
import { PasswordPolicyService } from '../../../../../core/services/password-policy.service';


@Component({
  selector: 'app-user-change-password',
  templateUrl: './user-change-password.component.html',
  styleUrl: './user-change-password.component.css',
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    ButtonComponent,
    IconComponent,
    NewPasswordFieldsComponent
  ]
})
export class UserChangePasswordComponent implements OnInit, HasUnsavedChanges {
  private readonly fb = inject(FormBuilder);
  private readonly userService = inject(UserService);
  private readonly modal = inject(NzModalRef);
  private readonly feedbackService = inject(FeedbackService);
  private readonly data = inject<{ entity: UserListItem }>(NZ_MODAL_DATA);
  private readonly passwordPolicyService = inject(PasswordPolicyService);

  readonly user = this.data.entity;
  readonly isLoading = signal(false);
  readonly passwordPolicy = signal<PasswordPolicyResponse | null>(null);
  readonly isPasswordPolicyLoading = signal(true);

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', Validators.required]
  });

  ngOnInit(): void {
    this.passwordPolicyService.policy$
      .pipe(finalize(() => this.isPasswordPolicyLoading.set(false)))
      .subscribe({
        next: policy => this.passwordPolicy.set(policy),
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty;
  }

  save(): void {
    if (this.isLoading()) {
      return;
    }

    if (!this.passwordPolicy() || this.form.invalid) {
      this.form.controls.newPassword.markAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.userService.changePassword({
      Id: this.user.Id,
      NewPassword: this.form.controls.newPassword.value,
      ConcurrencyStamp: this.user.ConcurrencyStamp
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.feedbackService.success('feedback.user.passwordChanged.title', 'feedback.user.passwordChanged.message');
          this.modal.close('success');
        },
        error: error => this.feedbackService.handleApiError(error)
      });
  }

  /** X, Cancel and Escape all land here so the unsaved-changes confirm can't be bypassed. */
  cancel(): void {
    if (!this.hasUnsavedChanges()) {
      this.modal.close();
      return;
    }

    this.feedbackService
      .confirm(
        'feedback.navigation.unsavedChanges.title',
        'feedback.navigation.unsavedChanges.message',
        { okLabelKey: 'common.discard', destructive: true }
      )
      .afterClose.pipe(take(1))
      .subscribe(result => {
        if (result === true) {
          this.modal.close();
        }
      });
  }
}
