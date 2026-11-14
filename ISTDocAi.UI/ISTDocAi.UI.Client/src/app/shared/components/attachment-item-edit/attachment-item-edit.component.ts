import { Component, OnInit, inject, signal } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { NzModalRef, NZ_MODAL_DATA } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzMessageService } from 'ng-zorro-antd/message';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AttachmentService } from '../../services/attachment.service';
import { AttachmentAddRequest, AttachmentUpdateRequest } from '../../models/attachment.model';
import { FeedbackService } from '../../../core/services/feedback.service';
import { Attachment } from '../attachment-list/attachment-list.component';
import { IconComponent } from '../../icons/icon.component';

@Component({
  selector: 'app-attachment-item-edit',
  imports: [
    FormsModule,
    NzButtonModule,
    NzInputModule,
    NzFormModule,
    IconComponent,
    TranslatePipe
],
  templateUrl: './attachment-item-edit.component.html',
  styleUrls: ['./attachment-item-edit.component.css']
})
export class AttachmentItemEditComponent implements OnInit {
  readonly modalRef = inject(NzModalRef);
  readonly nzModalData = inject(NZ_MODAL_DATA);

  mode: 'add' | 'edit' = 'add';
  referenceType = '';
  referenceId = '';
  referenceSerial = '';
  
  attachmentId?: string;
  rowVersion?: string;
  readonly fileName = signal<string>('');
  readonly note = signal<string>('');
  selectedFile: File | null = null;
  readonly loading = signal<boolean>(false);

  private message = inject(NzMessageService);
  private attachmentService = inject(AttachmentService);
  private feedbackService = inject(FeedbackService);
  private translate = inject(TranslateService);

  ngOnInit(): void {
    this.mode = this.nzModalData?.mode || 'add';
    this.referenceType = this.nzModalData?.referenceType || '';
    this.referenceId = this.nzModalData?.referenceId || '';
    this.referenceSerial = this.nzModalData?.referenceSerial || '';
    
    if (this.mode === 'edit' && this.nzModalData?.attachment) {
      const attachment = this.nzModalData.attachment as Attachment;
      this.attachmentId = attachment.Id;
      this.rowVersion = attachment.RowVersion;
      this.fileName.set(attachment.OriginalFileName || '');
      this.note.set(attachment.Note || '');
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectedFile = input.files[0];
      this.fileName.set(this.selectedFile.name);
    }
  }

  onSave(): void {
    if (this.mode === 'add' && !this.selectedFile) {
      this.message.error(this.translate.instant('shared.attachment.selectFile'));
      return;
    }

    if (!this.fileName().trim()) {
      this.message.error(this.translate.instant('shared.attachment.enterFileName'));
      return;
    }

    if (!this.referenceType || !this.referenceId || !this.referenceSerial) {
      this.message.error(this.translate.instant('shared.attachment.missingReference'));
      return;
    }

    this.loading.set(true);

    if (this.mode === 'add') {
      const request: AttachmentAddRequest = {
        originalFileName: this.fileName(),
        note: this.note(),
        referenceType: this.referenceType,
        referenceId: this.referenceId,
        referenceSerial: this.referenceSerial,
        file: this.selectedFile!
      };

      this.attachmentService.add(request).subscribe({
        next: () => {
          this.feedbackService.success('feedback.attachment.added.title', 'feedback.attachment.added.message');
          this.modalRef.close(true);
        },
        error: (error) => {
          console.error('Error adding attachment:', error);
          this.feedbackService.handleApiError(error);
          this.loading.set(false);
        }
      });
    } else {
      if (!this.attachmentId || !this.rowVersion) {
        this.message.error(this.translate.instant('shared.attachment.missingAttachment'));
        this.loading.set(false);
        return;
      }

      const request: AttachmentUpdateRequest = {
        id: this.attachmentId,
        rowVersion: this.rowVersion,
        originalFileName: this.fileName(),
        note: this.note(),
        referenceType: this.referenceType,
        referenceId: this.referenceId,
        referenceSerial: this.referenceSerial,
        file: this.selectedFile || undefined
      };

      this.attachmentService.update(request).subscribe({
        next: () => {
          this.feedbackService.success('feedback.attachment.updated.title', 'feedback.attachment.updated.message');
          this.modalRef.close(true);
        },
        error: (error) => {
          console.error('Error updating attachment:', error);
          this.feedbackService.handleApiError(error);
          this.loading.set(false);
        }
      });
    }
  }

  onCancel(): void {
    this.modalRef.close(null);
  }

  triggerFileInput(): void {
    const fileInput = document.getElementById('fileInput') as HTMLInputElement;
    if (fileInput) {
      fileInput.click();
    }
  }
}
