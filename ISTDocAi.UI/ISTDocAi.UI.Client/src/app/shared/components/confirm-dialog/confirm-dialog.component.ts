import { Component, inject } from '@angular/core';
import { NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../../icons/icon.component';

/** Already-translated strings; FeedbackService resolves the i18n keys. */
export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  /** Red confirm button for irreversible actions (delete, discard, clear). */
  destructive: boolean;
}

/**
 * Shared confirmation dialog - implements "Delete user dialog2" from the
 * ISTDocAi Figma file (node 1370:9202). Every confirm in the app
 * (delete users/roles, clear all permissions, discard unsaved changes, ...)
 * renders this one body; only the copy and the button parameters vary.
 *
 * Open it through FeedbackService.confirm(), not directly - the service owns
 * the NzModal options (no header/footer, width, class) so callers stay
 * consistent. Closes with `true` on confirm, `undefined` on cancel/X/Esc.
 */
@Component({
  selector: 'app-confirm-dialog',
  imports: [ButtonComponent, IconComponent],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.css'
})
export class ConfirmDialogComponent {
  readonly data = inject<ConfirmDialogData>(NZ_MODAL_DATA);
  private readonly modalRef = inject(NzModalRef);

  confirm(): void {
    this.modalRef.close(true);
  }

  cancel(): void {
    this.modalRef.close();
  }
}
