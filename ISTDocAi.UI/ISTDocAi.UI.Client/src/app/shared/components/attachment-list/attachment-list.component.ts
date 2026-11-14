import { Component, OnInit, computed, inject, input, signal } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule, NZ_MODAL_DATA } from 'ng-zorro-antd/modal';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { ModalManagementService } from '../../../core/services/modal-management.service';
import { AttachmentItemEditComponent } from '../attachment-item-edit/attachment-item-edit.component';
import { AttachmentService } from '../../services/attachment.service';
import { AttachmentResponse, AttachmentDeleteRequest } from '../../models/attachment.model';
import { FeedbackService } from '../../../core/services/feedback.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { IconComponent } from '../../icons/icon.component';


export interface Attachment extends Partial<AttachmentResponse> {
  selected?: boolean;
}

@Component({
  selector: 'app-attachment-list',
  imports: [
    FormsModule,
    NzTableModule,
    NzButtonModule,
    NzModalModule,
    NzCheckboxModule,
    NzTooltipModule,
    IconComponent,
    TranslatePipe
],
  templateUrl: './attachment-list.component.html',
  styleUrls: ['./attachment-list.component.css']
})
export class AttachmentListComponent implements OnInit {
  private modalManagementService = inject(ModalManagementService);
  private attachmentService = inject(AttachmentService);
  private feedbackService = inject(FeedbackService);
  private translate = inject(TranslateService);

  readonly referenceType = input<string>();
  readonly referenceId = input<string>();
  readonly referenceSerial = input<string>();
  readonly readOnly = input(false);

  readonly nzModalData = inject(NZ_MODAL_DATA, { optional: true });

  /** Inputs when used inline; the modal data when opened through AttachmentModalService. */
  private readonly reference = computed(() => {
    if (!this.referenceType() && this.nzModalData) {
      return {
        type: this.nzModalData.referenceType as string,
        id: this.nzModalData.referenceId as string,
        serial: this.nzModalData.referenceSerial as string,
        readOnly: (this.nzModalData.readOnly ?? false) as boolean
      };
    }
    return {
      type: this.referenceType() ?? '',
      id: this.referenceId() ?? '',
      serial: this.referenceSerial() ?? '',
      readOnly: this.readOnly()
    };
  });

  readonly isReadOnly = computed(() => this.reference().readOnly);

  readonly attachments = signal<Attachment[]>([]);
  readonly allSelected = signal(false);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.loadAttachments();
  }

  loadAttachments(): void {
    const { type, id } = this.reference();
    if (!type || !id) {
      return;
    }

    this.loading.set(true);
    this.attachmentService.getList(type, id).subscribe({
      next: attachments => {
        this.attachments.set(attachments.map(att => ({
          ...att,
          selected: false
        })));
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading attachments:', error);
        this.feedbackService.handleApiError(error);
        this.loading.set(false);
      }
    });
  }

  onAddAttachment(): void {
    const { type, id, serial } = this.reference();
    const modalRef = this.modalManagementService.openModal({
      title: 'shared.attachment.title.add',
      component: AttachmentItemEditComponent,
      data: {
        mode: 'add',
        referenceType: type,
        referenceId: id,
        referenceSerial: serial
      },
      width: 600,
      nzFooter: false
    }, 'add');

    modalRef.afterClose.subscribe((success: boolean) => {
      if (success) {
        this.loadAttachments();
      }
    });
  }

  onEditAttachment(attachment: Attachment): void {
    const { type, id, serial } = this.reference();
    const modalRef = this.modalManagementService.openModal({
      title: 'shared.attachment.title.edit',
      component: AttachmentItemEditComponent,
      data: {
        mode: 'edit',
        attachment: { ...attachment },
        referenceType: type,
        referenceId: id,
        referenceSerial: serial
      },
      width: 600,
      nzFooter: false
    }, 'edit');

    modalRef.afterClose.subscribe((success: boolean) => {
      if (success) {
        this.loadAttachments();
      }
    });
  }

  onDeleteAttachment(attachment: Attachment): void {
    if (!attachment.Id || !attachment.RowVersion) {
      return;
    }

    this.feedbackService.confirm(
      'shared.attachment.title.delete',
      'shared.attachment.confirmDelete',
      () => {
        const request: AttachmentDeleteRequest = {
          items: [{
            id: attachment.Id!,
            rowVersion: attachment.RowVersion!
          }]
        };

        this.attachmentService.delete(request).subscribe({
          next: () => {
            this.feedbackService.success('feedback.attachment.deleted.title', 'feedback.attachment.deleted.message');
            this.loadAttachments();
          },
          error: (error) => {
            console.error('Error deleting attachment:', error);
            this.feedbackService.handleApiError(error);
          }
        });
        return true;
      },
      { params: { name: attachment.OriginalFileName }, okLabelKey: 'common.delete', destructive: true }
    );
  }

  onViewAttachment(attachment: Attachment): void {
    if (!attachment.Id) {
      this.feedbackService.warning('feedback.attachment.notFound.title', 'feedback.attachment.notFound.message');
      return;
    }

    this.attachmentService.getFile(attachment.Id, false).subscribe({
      next: (blob) => {
        const blobUrl = window.URL.createObjectURL(blob);
        window.open(blobUrl, '_blank');

        // Clean up the blob URL after a short delay
        setTimeout(() => {
          window.URL.revokeObjectURL(blobUrl);
        }, 100);
      },
      error: (error) => {
        console.error('Error loading file:', error);
        this.feedbackService.handleApiError(error);
      }
    });
  }

  onToggleSelectAll(selected: boolean): void {
    this.attachments.update(attachments => attachments.map(attachment => ({ ...attachment, selected })));
  }

  onAttachmentSelectionChange(): void {
    // Update allSelected based on current selection state
    const attachments = this.attachments();
    this.allSelected.set(attachments.length > 0 && attachments.every(a => a.selected));
  }

  hasSelectedAttachments(): boolean {
    return this.attachments().some(a => a.selected);
  }

  onDeleteSelectedAttachments(): void {
    const selectedAttachments = this.attachments().filter(a => a.selected && a.Id && a.RowVersion);

    if (selectedAttachments.length === 0) {
      this.feedbackService.warning('feedback.attachment.noSelection.title', 'feedback.attachment.noSelection.message');
      return;
    }

    this.feedbackService.confirm(
      'shared.attachment.title.deleteMany',
      'shared.attachment.confirmDeleteMany',
      () => {
        const request: AttachmentDeleteRequest = {
          items: selectedAttachments.map(att => ({
            id: att.Id!,
            rowVersion: att.RowVersion!
          }))
        };

        this.attachmentService.delete(request).subscribe({
          next: result => {
            const deletedCount = result.deletedIds?.length || 0;
            const blockedCount = result.blocked?.length || 0;

            if (deletedCount > 0) {
              this.feedbackService.success('feedback.attachment.deletedMany.title', 'feedback.attachment.deletedMany.message', {
                messageParams: { count: deletedCount }
              });
            }
            if (blockedCount > 0) {
              this.feedbackService.warning('feedback.attachment.partiallyDeleted.title', 'feedback.attachment.partiallyDeleted.message', {
                messageParams: { count: blockedCount }
              });
            }

            this.allSelected.set(false);
            this.loadAttachments();
          },
          error: (error) => {
            console.error('Error deleting attachments:', error);
            this.feedbackService.handleApiError(error);
          }
        });
        return true;
      },
      { params: { count: selectedAttachments.length }, okLabelKey: 'common.delete', destructive: true }
    );
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return `0 ${this.translate.instant('shared.attachment.units.bytes')}`;

    const k = 1024;
    const sizes = [
      this.translate.instant('shared.attachment.units.bytes'),
      this.translate.instant('shared.attachment.units.kb'),
      this.translate.instant('shared.attachment.units.mb'),
      this.translate.instant('shared.attachment.units.gb')
    ];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }
}
