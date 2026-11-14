import { Component, OnInit, OnDestroy, inject, signal, DestroyRef, Signal, WritableSignal } from '@angular/core';

import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

// ng-zorro imports
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { TextFieldModule } from '@angular/cdk/text-field';
import { TranslatePipe } from '@ngx-translate/core';
// Services
import { SettingService } from '../../services/setting.service';
import { FeedbackService } from '../../../../core/services/feedback.service';
import { HasUnsavedChanges } from '../../../../core/models/has-unsaved-changes.model';

// Models
import { SettingResponse } from '../../models/setting.model';
import { SettingValidators } from './setting.validators';
import { IconComponent } from '../../../../shared/icons/icon.component';
import { ButtonComponent } from '../../../../shared/components/button/button.component';
import { formChanges } from '../../../../shared/utils/form-signal-utils';

// Interface for image state tracking
interface ImageState {
  currentPath: string | null;
  currentHash: string | null;
  newFile: File | null;
  newPreviewUrl: string | null;
  markedForDelete: boolean;
  loadedBlobUrl: string | null; // For API-loaded images
  currentBinaryData?: Uint8Array; // For storing binary data if needed
  currentContentType?: string; // For storing content type if needed
}

const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;
const ALLOWED_IMAGE_TYPES = new Set(['image/png', 'image/jpeg', 'image/svg+xml']);

@Component({
  selector: 'app-setting',
  templateUrl: './setting.component.html',
  styleUrl: './setting.component.scss',
  imports: [
    ReactiveFormsModule,
    NzFormModule,
    NzInputModule,
    NzSpinModule,
    NzTooltipModule,
    NzCheckboxModule,
    TextFieldModule,
    TranslatePipe,
    IconComponent,
    ButtonComponent
]
})
export class SettingComponent implements OnInit, OnDestroy, HasUnsavedChanges {
  private fb = inject(FormBuilder);
  private settingService = inject(SettingService);
  private feedback = inject(FeedbackService);

  form!: FormGroup;
  private readonly formState: Signal<unknown>;
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  
  // Setting data
  settingId = '';
  rowVersion = '';
  
  private readonly destroyRef = inject(DestroyRef);
  
  // Image states
  readonly logoState = signal<ImageState>(this.createEmptyImageState());
  readonly reportHeaderState = signal<ImageState>(this.createEmptyImageState());
  readonly reportFooterState = signal<ImageState>(this.createEmptyImageState());
  
  // Email password visibility
  readonly showEmailPassword = signal(false);
  readonly MASKED_PASSWORD = '********';
  
  // Base URL for images (same origin)
  private imageBaseUrl = '';

  constructor() {
    this.initForm();
    this.formState = formChanges(this.form);
  }

  ngOnInit(): void {
    this.loadSetting();
    this.setupPasswordFieldListener();
  }

  get isPasswordMasked(): boolean {
    this.formState();
    const passwordValue = this.form.get('emailPassword')?.value;
    return passwordValue === this.MASKED_PASSWORD;
  }

  private setupPasswordFieldListener(): void {
    this.form.get('emailPassword')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(value => {
        // If user modifies the masked password, reset the visibility toggle
        if (value !== this.MASKED_PASSWORD) {
          this.showEmailPassword.set(false);
        }
      });
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

  ngOnDestroy(): void {
    // Clean up preview URLs and blob URLs
    this.revokePreviewUrl(this.logoState());
    this.revokePreviewUrl(this.reportHeaderState());
    this.revokePreviewUrl(this.reportFooterState());
    this.revokeBlobUrl(this.logoState());
    this.revokeBlobUrl(this.reportHeaderState());
    this.revokeBlobUrl(this.reportFooterState());
  }

  /**
   * Consulted by unsavedChangesGuard. The form alone is not enough: image
   * uploads and deletions are staged outside the FormGroup.
   */
  hasUnsavedChanges(): boolean {
    return this.form.dirty || this.hasPendingImageChanges();
  }

  private hasPendingImageChanges(): boolean {
    return [this.logoState(), this.reportHeaderState(), this.reportFooterState()]
      .some(state => !!state.newFile || state.markedForDelete);
  }

  private initForm(): void {
    this.form = this.fb.group({
      companyNameEn: ['', SettingValidators.companyNameEn],
      companyNameAr: ['', SettingValidators.companyNameAr],
      countryId: [null],
      companyAddress: ['', SettingValidators.companyAddress],
      companyPhoneOne: ['', SettingValidators.companyPhoneOne],
      companyPhoneTwo: ['', SettingValidators.companyPhoneTwo],
      companyEmail: ['', SettingValidators.companyEmail],
      companyWebsite: ['', SettingValidators.companyWebsite],
      companyFax: ['', SettingValidators.companyFax],
      defaultCurrencyId: [null],
      // Email Configuration
      isEmailEnabled: [false, SettingValidators.isEmailEnabled],
      emailHost: [''],
      emailPort: [587],
      emailUsername: [''],
      emailPassword: [''],
      senderEmail: [''],
      emailEnableSSL: [true, SettingValidators.emailEnableSSL],
      AttachmentFolderPath: [''],
      AttachedFileMaxSize: [null],

    });
  }

  private createEmptyImageState(): ImageState {
    return {
      currentPath: null,
      currentHash: null,
      newFile: null,
      newPreviewUrl: null,
      markedForDelete: false,
      loadedBlobUrl: null
    };
  }

  onEmailEnabledChange(enabled: boolean): void {
    this.updateEmailValidators(enabled);
  }

  private updateEmailValidators(enabled: boolean): void {
    const emailFields = ['emailHost', 'emailPort', 'emailUsername', 'emailPassword', 'senderEmail'];
    
    emailFields.forEach(field => {
      const control = this.form.get(field);
      if (control) {
        if (enabled) {
          // Add validators based on field type
          switch (field) {
            case 'emailHost':
              control.setValidators(SettingValidators.emailHost);
              break;
            case 'emailPort':
              control.setValidators(SettingValidators.emailPort);
              break;
            case 'emailUsername':
              control.setValidators(SettingValidators.emailUsername);
              break;
            case 'emailPassword':
              control.setValidators(SettingValidators.emailPassword);
              break;
            case 'senderEmail':
              control.setValidators(SettingValidators.senderEmail);
              break;
          }
        } else {
          // Remove validators
          control.clearValidators();
        }
        control.updateValueAndValidity();
      }
    });
  }

  private loadSetting(): void {
    this.isLoading.set(true);
    this.settingService.get().subscribe({
      next: (setting) => {
        this.populateForm(setting);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.feedback.handleApiError(err);
        this.isLoading.set(false);
      }
    });
  }

  private populateForm(data: SettingResponse): void {
    this.settingId = data.Id;
    this.rowVersion = data.RowVersion;

    this.form.patchValue({
      companyNameEn: data.CompanyNameEn || '',
      companyNameAr: data.CompanyNameAr || '',
      countryId: data.CountryId || null,
      companyAddress: data.CompanyAddress || '',
      companyPhoneOne: data.CompanyPhoneOne || '',
      companyPhoneTwo: data.CompanyPhoneTwo || '',
      companyEmail: data.CompanyEmail || '',
      companyWebsite: data.CompanyWebsite || '',
      companyFax: data.CompanyFax || '',
      defaultCurrencyId: data.DefaultCurrencyId || null,
      // Email Configuration
      isEmailEnabled: data.IsEmailEnabled,
      emailHost: data.EmailHost || '',
      emailPort: data.EmailPort || 587,
      emailUsername: data.EmailUsername || '',
      emailPassword: data.EmailPassword || '',
      senderEmail: data.SenderEmail || '',
      emailEnableSSL: data.EmailEnableSSL ?? true,

      AttachmentFolderPath: data.AttachmentFolderPath ?? '',
      AttachedFileMaxSize: data.AttachedFileMaxSize ?? null,
    });

    // Update email field validators based on isEmailEnabled
    this.updateEmailValidators(data.IsEmailEnabled);

    // Set image states
    this.logoState.set({
      currentPath: data.CompanyLogoPath || null,
      currentHash: data.CompanyLogoHash || null,
      newFile: null,
      newPreviewUrl: null,
      markedForDelete: false,
      loadedBlobUrl: null,
      currentBinaryData: data.CompanyLogoBinaryData,
      currentContentType: data.CompanyLogoContentType
    });

    // Load logo from API if hash exists
    if (this.logoState().currentBinaryData && this.logoState().currentContentType) {
      this.loadLogoFromApi();
    }

    this.reportHeaderState.set({
      currentPath: data.ReportHeaderPath || null,
      currentHash: data.ReportHeaderHash || null,
      newFile: null,
      newPreviewUrl: null,
      markedForDelete: false,
      loadedBlobUrl: null,
      currentBinaryData: data.ReportHeaderBinaryData,
      currentContentType: data.ReportHeaderContentType
    });

    // Load report header from API if hash exists
    if (this.reportHeaderState().currentBinaryData && this.reportHeaderState().currentContentType) {
      this.loadReportHeaderFromApi();
    }

    this.reportFooterState.set({
      currentPath: data.ReportFooterPath || null,
      currentHash: data.ReportFooterHash || null,
      newFile: null,
      newPreviewUrl: null,
      markedForDelete: false,
      loadedBlobUrl: null,
      currentBinaryData: data.ReportFooterBinaryData,
      currentContentType: data.ReportFooterContentType
    });

    // Load report footer from API if hash exists
    if (this.reportFooterState().currentBinaryData && this.reportFooterState().currentContentType) {
      this.loadReportFooterFromApi();
    }

    // Form and image states now mirror persisted server state. Runs on initial
    // load and on the post-save reload, so a saved page stops reporting dirty.
    this.form.markAsPristine();
  }

  // Image handling methods
  private loadLogoFromApi(): void {
    this.loadImageFromApi(1, 'logo', 'logo');
  }

  private loadReportHeaderFromApi(): void {
    this.loadImageFromApi(2, 'reportHeader', 'report header');
  }

  private loadReportFooterFromApi(): void {
    this.loadImageFromApi(3, 'reportFooter', 'report footer');
  }

  private loadImageFromApi(imageType: number, imageKey: 'logo' | 'reportHeader' | 'reportFooter', imageName: string): void {
    this.settingService.getImage(imageType)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (blob) => {
          // Revoke old blob URL if exists
          this.revokeBlobUrl(this.getImageState(imageKey));
          // Create new object URL from blob
          this.patchImageState(imageKey, { loadedBlobUrl: URL.createObjectURL(blob) });
        },
        error: (err) => {
          console.error(`Failed to load ${imageName}:`, err);
        }
      });
  }

  getImagePreviewUrl(state: ImageState): string | null {
    if (state.markedForDelete) {
      return null;
    }
    if (state.newPreviewUrl) {
      return state.newPreviewUrl;
    }
    // For images loaded from API (all three types now use blob URLs)
    if (state.loadedBlobUrl) {
      return state.loadedBlobUrl;
    }
    // Fallback to file path if no blob URL
    if (state.currentPath) {
      const path = state.currentPath?.startsWith('/') ? state.currentPath : `/${state.currentPath}`;
      return path;
    }
    return null;
  }

  onFileSelected(event: Event, imageType: 'logo' | 'reportHeader' | 'reportFooter'): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) {
      return;
    }

    const file = input.files[0];

    if (!ALLOWED_IMAGE_TYPES.has(file.type)) {
      this.feedback.warning(
        'feedback.settings.unsupportedImageFormat.title',
        'feedback.settings.unsupportedImageFormat.message'
      );
      input.value = '';
      return;
    }

    if (file.size > MAX_IMAGE_SIZE_BYTES) {
      this.feedback.warning(
        'feedback.settings.imageTooLarge.title',
        'feedback.settings.imageTooLarge.message'
      );
      input.value = '';
      return;
    }

    const state = this.getImageState(imageType);

    // Clear previous preview
    this.revokePreviewUrl(state);

    // Set new file and preview
    this.patchImageState(imageType, {
      newFile: file,
      newPreviewUrl: URL.createObjectURL(file),
      markedForDelete: false
    });

    // Reset input
    input.value = '';
  }

  onDeleteImage(imageType: 'logo' | 'reportHeader' | 'reportFooter'): void {
    const state = this.getImageState(imageType);
    
    // Clear any new file
    this.revokePreviewUrl(state);
    this.patchImageState(imageType, {
      newFile: null,
      newPreviewUrl: null,
      // Mark for deletion only if there's an existing image
      markedForDelete: state.currentBinaryData || state.currentPath || state.currentHash
        ? true
        : state.markedForDelete
    });
  }

  onCancelDelete(imageType: 'logo' | 'reportHeader' | 'reportFooter'): void {
    this.patchImageState(imageType, { markedForDelete: false });
  }

  private getImageState(imageType: 'logo' | 'reportHeader' | 'reportFooter'): ImageState {
    return this.imageStateSignal(imageType)();
  }

  private imageStateSignal(imageType: 'logo' | 'reportHeader' | 'reportFooter'): WritableSignal<ImageState> {
    switch (imageType) {
      case 'logo':
        return this.logoState;
      case 'reportHeader':
        return this.reportHeaderState;
      case 'reportFooter':
        return this.reportFooterState;
    }
  }

  private patchImageState(imageType: 'logo' | 'reportHeader' | 'reportFooter', patch: Partial<ImageState>): void {
    this.imageStateSignal(imageType).update(state => ({ ...state, ...patch }));
  }

  private revokePreviewUrl(state: ImageState): void {
    if (state.newPreviewUrl) {
      URL.revokeObjectURL(state.newPreviewUrl);
    }
  }

  private revokeBlobUrl(state: ImageState): void {
    if (state.loadedBlobUrl) {
      URL.revokeObjectURL(state.loadedBlobUrl);
    }
  }

  hasImage(imageType: 'logo' | 'reportHeader' | 'reportFooter'): boolean {
    const state = this.getImageState(imageType);
    // Check for new preview, loaded blob URL, current path, or hash (all images now use API)
    return !state.markedForDelete && (!!state.newPreviewUrl || !!state.loadedBlobUrl || !!state.currentPath || !!state.currentHash);
  }

  isMarkedForDelete(imageType: 'logo' | 'reportHeader' | 'reportFooter'): boolean {
    return this.getImageState(imageType).markedForDelete;
  }

  canDownloadImage(imageType: 'logo' | 'reportHeader' | 'reportFooter'): boolean {
    const state = this.getImageState(imageType);
    // Can download if there's either a new file selected or an existing image on the server
    return !state.markedForDelete && (!!state.newFile || !!state.currentHash || !!state.currentPath);
  }

  downloadImage(imageType: 'logo' | 'reportHeader' | 'reportFooter'): void {
    const state = this.getImageState(imageType);
    
    if (state.newFile) {
      // Download the newly selected file
      const url = URL.createObjectURL(state.newFile);
      this.triggerDownload(url, state.newFile.name);
      URL.revokeObjectURL(url);
    } else if (state.currentBinaryData || state.currentHash || state.currentPath) {
      // Download from server using API endpoint
      this.downloadFromServer(imageType);
    }
  }

  private downloadFromServer(imageType: 'logo' | 'reportHeader' | 'reportFooter'): void {
    // Map to ImageType enum: 1=Logo, 2=ReportHeader, 3=ReportFooter
    const imageTypeMap: Record<'logo' | 'reportHeader' | 'reportFooter', number> = {
      'logo': 1,
      'reportHeader': 2,
      'reportFooter': 3
    };

    const imageTypeValue = imageTypeMap[imageType];

    this.settingService.downloadImage(imageTypeValue)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ blob, filename }) => {
          const url = URL.createObjectURL(blob);
          this.triggerDownload(url, filename);
          URL.revokeObjectURL(url);
        },
        error: (err) => {
          console.error(`Failed to download ${imageType}:`, err);
          this.feedback.error(
            'feedback.settings.imageDownloadFailed.title',
            'feedback.settings.imageDownloadFailed.message',
            {
              messageParams: { imageType }
            }
          );
        }
      });
  }

  private triggerDownload(url: string, filename: string): void {
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  // Form submission
  onSave(): void {
    if (this.isSaving()) {
      return;
    }

    this.trimEmailFields();

    // Mark all controls as touched for validation display
    Object.keys(this.form.controls).forEach(key => {
      const control = this.form.get(key);
      control?.markAsTouched();
      control?.updateValueAndValidity();
    });

    if (this.form.invalid) {
      this.feedback.warning(
        'feedback.settings.requiredFields.title',
        'feedback.settings.requiredFields.message'
      );
      return;
    }

    this.isSaving.set(true);
    const formData = this.buildFormData();

    this.settingService.update(formData).subscribe({
      next: () => {
        this.feedback.success('feedback.settings.updated.title', 'feedback.settings.updated.message');
        this.loadSetting();
        this.isSaving.set(false);
      },
      error: (err) => {
        this.feedback.handleApiError(err);
        this.isSaving.set(false);
      }
    });
  }

  private trimEmailFields(): void {
    const mailFields = ['emailHost', 'emailUsername', 'senderEmail'];
    mailFields.forEach(fieldName => {
      const control = this.form.get(fieldName);
      const value = control?.value;

      if (typeof value === 'string') {
        control?.setValue(value.trim(), { emitEvent: false });
      }
    });
  }

  private buildFormData(): FormData {
    const formData = new FormData();
    const formValue = this.form.getRawValue();

    // Required fields
    formData.append('Id', this.settingId);
    formData.append('RowVersion', this.rowVersion);
    formData.append('CompanyNameEn', formValue.companyNameEn || '');
    formData.append('CompanyAddress', formValue.companyAddress || '');
    formData.append('CompanyPhoneOne', formValue.companyPhoneOne || '');
    formData.append('DefaultCurrencyId', formValue.defaultCurrencyId || '');

    formData.append('AttachmentFolderPath', formValue.AttachmentFolderPath || '');
    formData.append('AttachedFileMaxSize', formValue.AttachedFileMaxSize?.toString() || '');


    // Optional fields
    if (formValue.companyNameAr) {
      formData.append('CompanyNameAr', formValue.companyNameAr);
    }
    if (formValue.countryId) {
      formData.append('CountryId', formValue.countryId);
    }
    if (formValue.companyPhoneTwo) {
      formData.append('CompanyPhoneTwo', formValue.companyPhoneTwo);
    }
    if (formValue.companyEmail) {
      formData.append('CompanyEmail', formValue.companyEmail);
    }
    if (formValue.companyWebsite) {
      formData.append('CompanyWebsite', formValue.companyWebsite);
    }
    if (formValue.companyFax) {
      formData.append('CompanyFax', formValue.companyFax);
    }

    // Handle images
    this.appendImageToFormData(formData, this.logoState(), 'LogoFile', 'DeleteLogo');
    this.appendImageToFormData(formData, this.reportHeaderState(), 'ReportHeaderFile', 'DeleteReportHeader');
    this.appendImageToFormData(formData, this.reportFooterState(), 'ReportFooterFile', 'DeleteReportFooter');

    // Email Configuration
    formData.append('IsEmailEnabled', formValue.isEmailEnabled ? 'true' : 'false');
    if (formValue.emailHost) {
      formData.append('EmailHost', formValue.emailHost);
    }
    if (formValue.emailPort) {
      formData.append('EmailPort', formValue.emailPort.toString());
    }
    if (formValue.emailUsername) {
      formData.append('EmailUsername', formValue.emailUsername);
    }
    // Only send password if it's not the masked value (user actually changed it)
    if (formValue.emailPassword && formValue.emailPassword !== this.MASKED_PASSWORD) {
      formData.append('EmailPassword', formValue.emailPassword);
    }
    if (formValue.senderEmail) {
      formData.append('SenderEmail', formValue.senderEmail);
    }
    formData.append('EmailEnableSSL', formValue.emailEnableSSL ? 'true' : 'false');

    return formData;
  }

  private appendImageToFormData(
    formData: FormData,
    state: ImageState,
    fileFieldName: string,
    deleteFieldName: string
  ): void {
    // If marked for delete → send delete flag (don't send file)
    if (state.markedForDelete) {
      formData.append(deleteFieldName, 'true');
      return;
    }

    // If new file selected → send the file
    if (state.newFile) {
      formData.append(fileFieldName, state.newFile, state.newFile.name);
    }
    // If no new file and not deleting → backend keeps existing image (no action needed)
  }

  // Helper for form validation display
  isFieldInvalid(fieldName: string): boolean {
    this.formState();
    const control = this.form.get(fieldName);
    return control ? control.invalid && control.touched : false;
  }

  getFieldError(fieldName: string): string {
    this.formState();
    const control = this.form.get(fieldName);
    if (!control || !control.errors) return '';

    // Get the first error key
    const errorKey = Object.keys(control.errors)[0];
    
    // Use SettingValidators to get the error message
    return SettingValidators.getErrorMessage(fieldName, errorKey);
  }

  // private clearCache(): void {
  //   // Clear Service Worker caches
  //   if ('caches' in window) {
  //     caches.keys().then((names) => {
  //       names.forEach((name) => caches.delete(name));
  //     }).then(() => {
  //       // Hard reload after clearing caches
  //       (window as Window).location.reload();
  //     });
  //   } else {
  //     // Fallback: just hard reload
  //     (window as Window).location.reload();
  //   }
  // }
}
