import { Component, OnInit, inject, Signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';

import { NzModalRef, NZ_MODAL_DATA } from 'ng-zorro-antd/modal';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { IconComponent } from '../../../../../shared/icons/icon.component';
import { RoleService } from '../../../services/role.service';
import { RoleValidators } from './role-edit.validators';
import { TranslatePipe } from '@ngx-translate/core';
import { SaveableComponent } from '../../../../../core/models/modal-config.model';
import { HasUnsavedChanges } from '../../../../../core/models/has-unsaved-changes.model';
import { GetRoleResponse } from '../../../models/role-list-item.model';
import { generateIdempotencyKey } from '../../../../../shared/utils/idempotencykey-utils';
import { trimObjectStrings } from '../../../../../shared/utils/object-utils';
import { FeedbackService } from '../../../../../core/services/feedback.service';

import { formChanges } from '../../../../../shared/utils/form-signal-utils';
@Component({
  selector: 'app-role-edit',
  templateUrl: './role-edit.component.html',
  styleUrl: './role-edit.component.css',
  imports: [
    ReactiveFormsModule,
    NzCheckboxModule,
    IconComponent,
    TranslatePipe
]
})
export class RoleEditComponent implements OnInit, SaveableComponent, HasUnsavedChanges {
  private fb = inject(FormBuilder);
  private roleService = inject(RoleService);
  private modal = inject(NzModalRef);
  private feedbackService = inject(FeedbackService);
  data = inject(NZ_MODAL_DATA);

  form: FormGroup;
  private readonly formState: Signal<unknown>;
  mode: 'view' | 'add' | 'edit';
  role: GetRoleResponse | null = null;
  idempotencyKey: string | null = null;
  isViewMode = false;

  constructor() {
    const data = this.data;

    this.mode = data.mode;
    this.role = data.entity || null;
    this.isViewMode = data.isViewMode || this.mode === 'view';

    // Initialize form with proper data mapping
    this.form = this.fb.group({
      nameEn: ['', RoleValidators.nameEn],
      nameAr: [''],
      isActive: [true],
      displayOrder: [0]
    });
    this.formState = formChanges(this.form);

    // If viewing or editing, populate form with role data
    if ((this.mode === 'view' || this.mode === 'edit') && this.role) {
      this.populateForm();
    }

    // Disable form if in view mode
    if (this.isViewMode) {
      this.form.disable();
    }
  }

  ngOnInit() {
    if (this.mode === 'add') {
      this.idempotencyKey = generateIdempotencyKey();
    }
  }

  /** Increments/decrements a numeric FormControl, clamped to [min, max] -
   * drives the custom stepper buttons that replaced nz-input-number. */
  stepNumberField(fieldName: string, delta: number, min: number, max = Infinity): void {
    const control = this.form.get(fieldName);
    if (!control) {
      return;
    }
    const current = Number(control.value) || 0;
    const next = Math.min(max, Math.max(min, current + delta));
    control.setValue(next);
    control.markAsTouched();
  }

  /**
   * Populate form with role data from server
   */
  private populateForm() {
    if (this.role) {
      this.form.patchValue({
        nameEn: this.role.NameEn || '',
        nameAr: this.role.NameAr || '',
        isActive: this.role.IsActive ?? true,
        displayOrder: this.role.DisplayOrder ?? 0
      });
    }
  }

  save() {
    // Prevent save in view mode
    if (this.isViewMode) return;

    // validate
    Object.keys(this.form.controls).forEach(key => {
      const control = this.form.get(key);
      control?.markAsTouched();
    });
    if (this.form.invalid) return;

    // save
    const value = trimObjectStrings(this.form.value);
    if (this.mode === 'add') {
      this.roleService.add({
        Name: value.nameEn || '', // Name will be set to NameEn in backend
        NameEn: value.nameEn || '',
        NameAr: value.nameAr || '',
        IsActive: value.isActive ?? true,
        DisplayOrder: value.displayOrder ?? 0
      }, this.idempotencyKey).subscribe({
        next: () => {
          this.feedbackService.success('feedback.role.created.title', 'feedback.role.created.message');
          this.modal.close('success');
        },
        error: (error) => {
          console.error('Failed to add role:', error);
          this.feedbackService.handleApiError(error);
        }
      });
    } else {
      if (!this.role) {
        console.error('Role data is unavailable for editing.');
        return;
      }
      
      this.roleService.update({
        Id: this.role.Id,
        Name: value.nameEn || '', // Name will be set to NameEn in backend
        NameEn: value.nameEn || '',
        NameAr: value.nameAr || '',
        IsActive: value.isActive ?? true,
        DisplayOrder: value.displayOrder ?? 0,
        ConcurrencyStamp: this.role.ConcurrencyStamp
      }).subscribe({
        next: () => {
          this.feedbackService.success('feedback.role.updated.title', 'feedback.role.updated.message');
          this.modal.close('success');
        },
        error: (error) => {
          console.error('Failed to update role:', error);
          this.feedbackService.handleApiError(error);
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
    if (this.form.invalid) return;

    const value = trimObjectStrings(this.form.value);
    if (this.mode === 'add') {
      this.roleService.add({
        Name: value.nameEn || '', // Name will be set to NameEn in backend
        NameEn: value.nameEn || '',
        NameAr: value.nameAr || '',
        IsActive: value.isActive ?? true,
        DisplayOrder: value.displayOrder ?? 0
      }, this.idempotencyKey).subscribe({
        next: () => {
          this.feedbackService.success('feedback.role.created.title', 'feedback.role.created.message');
          this.form.reset();
          this.idempotencyKey = generateIdempotencyKey();
          this.roleService.notifyAdded();
        },
        error: (error) => {
          console.error('Failed to add role:', error);
          this.feedbackService.handleApiError(error);
        }
      });
    }
  }

  /**
   * Get error message for a form control
   */
  getErrorMessage(controlName: string): string {
    this.formState();
    const control = this.form.get(controlName);
    if (control?.invalid && control?.touched) {
      const errorKey = Object.keys(control.errors || {})[0];
      return RoleValidators.getErrorMessage(controlName, errorKey);
    }
    return '';
  }
}




