import { Component, OnInit, inject, signal, Signal } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { NzModalRef, NZ_MODAL_DATA } from 'ng-zorro-antd/modal';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzSelectOptionInterface } from 'ng-zorro-antd/select';
import { SelectComponent } from '../../../../../shared/components/select/select.component';
import { NewPasswordFieldsComponent } from '../../../../../shared/components/new-password-fields/new-password-fields.component';
import { UserService } from '../../../services/user.service';
import { RoleService } from '../../../services/role.service';
import { RoleListItem } from '../../../models/role-list-item.model';
import { UserValidators } from './user-edit.validators';
import { TranslatePipe } from '@ngx-translate/core';
import { SaveableComponent } from '../../../../../core/models/modal-config.model';
import { HasUnsavedChanges } from '../../../../../core/models/has-unsaved-changes.model';
import { UserAddCommand } from '../../../models/user-list-item.model';
import { GetUserResponse } from '../../../models/user-list-item.model';
import { generateIdempotencyKey } from '../../../../../shared/utils/idempotencykey-utils';
import { trimObjectStrings } from '../../../../../shared/utils/object-utils';
import { FeedbackService } from '../../../../../core/services/feedback.service';
import { TranslateService } from '@ngx-translate/core';
import { extractApiFailure } from '../../../../../shared/utils/api-result.utils';

import { formChanges } from '../../../../../shared/utils/form-signal-utils';
import { PasswordPolicyResponse } from '../../../../../core/models/auth.model';
import { PasswordPolicyService } from '../../../../../core/services/password-policy.service';
@Component({
  selector: 'app-user-edit',
  templateUrl: './user-edit.component.html',
  styleUrl: './user-edit.component.css',
  imports: [
    ReactiveFormsModule,
    NzCheckboxModule,
    SelectComponent,
    NewPasswordFieldsComponent,
    TranslatePipe
]
})
export class UserEditComponent implements OnInit, SaveableComponent, HasUnsavedChanges {
  private fb = inject(FormBuilder);
  private userService = inject(UserService);
  private roleService = inject(RoleService);
  private modal = inject(NzModalRef);
  private feedbackService = inject(FeedbackService);
  private translate = inject(TranslateService);
  private readonly passwordPolicyService = inject(PasswordPolicyService);
  data = inject(NZ_MODAL_DATA);

  form: FormGroup;
  private readonly formState: Signal<unknown>;
  mode: 'view' | 'add' | 'edit';
  user: GetUserResponse | null = null;
  idempotencyKey: string | null = null;
  isViewMode = false;
  roles: RoleListItem[] = [];
  readonly loadingRoles = signal(false);
  readonly passwordPolicy = signal<PasswordPolicyResponse | null>(null);
  readonly isPasswordPolicyLoading = signal(false);

  readonly roleOptions = signal<NzSelectOptionInterface[]>([]);

  constructor() {
    const data = this.data;

    this.mode = data.mode;
    this.user = data.entity || null;
    this.isViewMode = data.isViewMode || this.mode === 'view';

    this.form = this.fb.group({
      userName: ['', UserValidators.userName],
      fullName: ['', UserValidators.fullName],
      email: ['', UserValidators.email],
      password: [''],
      isActive: [true],
      isAdministrator: [false],
      roleId: [null, UserValidators.roleId]
    });
    this.formState = formChanges(this.form);

    if ((this.mode === 'view' || this.mode === 'edit') && this.user) {
      this.populateForm();
      this.form.get('password')?.disable();
    }

    if (this.isViewMode) {
      this.form.disable();
    }
  }

  ngOnInit() {
    if (this.mode === 'add') {
      this.idempotencyKey = generateIdempotencyKey();
      this.isPasswordPolicyLoading.set(true);
      this.passwordPolicyService.policy$
        .pipe(finalize(() => this.isPasswordPolicyLoading.set(false)))
        .subscribe({
          next: policy => this.passwordPolicy.set(policy),
          error: error => this.feedbackService.handleApiError(error)
        });
    }
    this.loadRoles();
  }

  get passwordControl(): FormControl<string> {
    return this.form.controls['password'] as FormControl<string>;
  }

  loadRoles(): void {
    this.loadingRoles.set(true);
    this.roleService.getList({
      PageNo: 1,
      PageRowCount: 10000,
      SortField: 'DisplayOrder',
      SortOrder: 'asc'
    }).subscribe({
      next: (result) => {
        this.roles = result.Items.filter(r => r.IsActive);
        this.roleOptions.set(this.roles.map(role => ({
          value: role.Id,
          label: (role.NameEn || '').trim() || (role.NameAr || '').trim() || this.translate.instant('role.table.notAvailable')
        })));
        this.loadingRoles.set(false);
      },
      error: (error) => {
        console.error('Failed to load roles:', error);
        this.loadingRoles.set(false);
      }
    });
  }

  private populateForm() {
    if (this.user) {
      this.form.patchValue({
        userName: this.user.UserName || '',
        fullName: this.user.FullName || '',
        email: this.user.Email || '',
        isActive: this.user.IsActive ?? true,
        isAdministrator: this.user.IsAdministrator ?? false,
        roleId: this.user.RoleId || null
      });
    }
  }

  save() {
    if (this.isViewMode) return;

    Object.keys(this.form.controls).forEach(key => {
      const control = this.form.get(key);
      control?.markAsTouched();
    });
    if (this.mode === 'add' && !this.passwordPolicy()) return;
    if (this.form.invalid) return;

    const value = trimObjectStrings(this.form.value);
    if (this.mode === 'add') {
      this.userService.add({
        UserName: value.userName,
        FullName: value.fullName,
        Email: value.email,
        Password: value.password,
        IsActive: value.isActive ?? true,
        IsAdministrator: value.isAdministrator ?? false,
        RoleId: value.roleId || undefined
      } as UserAddCommand, this.idempotencyKey).subscribe({
        next: () => {
          this.feedbackService.success('feedback.user.created.title', 'feedback.user.created.message');
          this.modal.close('success');
        },
        error: (error) => {
          console.error('Failed to add user:', error);
          this.handleInlineApiErrors(error);
        }
      });
    } else {
      if (!this.user) {
        console.error('User data is unavailable for editing.');
        return;
      }

      this.userService.update({
        Id: this.user.Id,
        UserName: value.userName,
        Email: value.email,
        FullName: value.fullName,
        IsActive: value.isActive ?? true,
        IsAdministrator: value.isAdministrator ?? false,
        RoleId: value.roleId || undefined,
        ConcurrencyStamp: this.user.ConcurrencyStamp
      }).subscribe({
        next: () => {
          this.feedbackService.success('feedback.user.updated.title', 'feedback.user.updated.message');
          this.modal.close('success');
        },
        error: (error) => {
          console.error('Failed to update user:', error);
          this.handleInlineApiErrors(error);
        }
      });
    }
  }

  cancel() {
    this.modal.close();
  }

  /** Consulted by ModalManagementService before closing the modal. */
  hasUnsavedChanges(): boolean {
    return this.form.dirty;
  }

  saveAndNew() {
    Object.keys(this.form.controls).forEach(key => {
      const control = this.form.get(key);
      control?.markAsTouched();
    });
    if (!this.passwordPolicy()) return;
    if (this.form.invalid) return;

    const value = trimObjectStrings(this.form.value);
    if (this.mode === 'add') {
      this.userService.add({
        UserName: value.userName,
        FullName: value.fullName,
        Email: value.email,
        Password: value.password,
        IsActive: value.isActive ?? true,
        IsAdministrator: value.isAdministrator ?? false,
        RoleId: value.roleId || undefined
      } as UserAddCommand, this.idempotencyKey).subscribe({
        next: () => {
          this.feedbackService.success('feedback.user.created.title', 'feedback.user.created.message');
          this.form.reset({ isActive: true, isAdministrator: false });
          this.idempotencyKey = generateIdempotencyKey();
          this.userService.notifyAdded();
        },
        error: (error) => {
          console.error('Failed to add user:', error);
          this.handleInlineApiErrors(error);
        }
      });
    }
  }

  /**
   * Put server-side field errors on the form and show non-field failures as feedback.
   */
  private handleInlineApiErrors(source: unknown): void {
    const failure = extractApiFailure(source);
    if (!failure) {
      this.feedbackService.handleApiError(source);
      return;
    }

    const errors = failure.Errors?.length
      ? failure.Errors
      : failure.ValidationErrors?.length
        ? failure.ValidationErrors
        : [failure.Error];
    let hasFieldError = false;

    const messagesByField = new Map<string, string[]>();
    errors.forEach(error => {
      const fieldName = this.extractFieldName(error.Code);
      if (fieldName && this.form.get(fieldName)) {
        const messages = messagesByField.get(fieldName) ?? [];
        messages.push(error.Message);
        messagesByField.set(fieldName, messages);
      }
    });

    messagesByField.forEach((messages, fieldName) => {
      const control = this.form.get(fieldName);
      if (control) {
        control.setErrors({ server: messages.join('\n') });
        control.markAsTouched();
        hasFieldError = true;
      }
    });

    if (!hasFieldError) {
      this.feedbackService.handleApiError(source);
    }
  }

  /**
   * Extract field name from error code (e.g., "User.DuplicateUserName" -> "userName")
   */
  private extractFieldName(errorCode: string): string | null {
    if (errorCode.includes('UserName')) return 'userName';
    if (errorCode.includes('Email')) return 'email';
    if (errorCode.includes('Password')) return 'password';
    if (errorCode.includes('Role')) return 'roleId';
    return null;
  }

  /**
   * Get error message for a form control
   */
  getErrorMessage(controlName: string): string {
    this.formState();
    const control = this.form.get(controlName);
    if (control?.invalid && control?.touched) {
      const errors = control.errors || {};
      const errorKeys = Object.keys(errors);

      if (errorKeys.length > 0) {
        if (errorKeys[0] === 'server') {
          return errors['server'];
        }
        return UserValidators.getErrorMessage(controlName, errorKeys[0]);
      }
    }
    return '';
  }
}
