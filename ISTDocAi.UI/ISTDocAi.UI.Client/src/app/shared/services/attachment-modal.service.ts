import { Injectable, inject } from '@angular/core';
import { NzModalRef } from 'ng-zorro-antd/modal';
import { ModalManagementService } from '../../core/services/modal-management.service';
import { AttachmentListComponent } from '../components/attachment-list/attachment-list.component';
import { FeedbackService } from '../../core/services/feedback.service';

@Injectable({
  providedIn: 'root'
})
export class AttachmentModalService {
  private readonly modalManagementService = inject(ModalManagementService);
  private readonly feedbackService = inject(FeedbackService);

  /**
   * Opens the attachment list modal
   * @param referenceType The type of entity the attachments belong to (e.g., 'Document', 'Record')
   * @param referenceId The ID of the entity the attachments belong to
   * @param referenceSerial The serial number of the entity the attachments belong to
   * @param title Optional custom title for the modal (default: 'Attachments')
   * @param width Optional custom width for the modal (default: 1000)
   * @param readOnly Optional flag to make the modal read-only (default: false)
   * @returns NzModalRef reference to the opened modal
   */
  openAttachmentModal(
    referenceType: string, 
    referenceId: string, 
    referenceSerial: string,
    title = 'shared.attachment.title.attachments',
    width = 1000,
    readOnly = false
  ): NzModalRef | null {
    if(referenceId==null || referenceId==undefined || referenceId==''){
      this.feedbackService.warning('feedback.attachment.saveBeforeManage.title', 'feedback.attachment.saveBeforeManage.message', {
        messageParams: { referenceType }
      });
      return null;
    }
    const modalRef = this.modalManagementService.openModal({
      title: title,
      component: AttachmentListComponent,
      data: { 
        referenceType: referenceType,
        referenceId: referenceId,
        referenceSerial: referenceSerial,
        readOnly: readOnly
      },
      width: width,
      nzFooter: false
    }, 'view');

    // The list reads referenceType/Id/Serial/readOnly from NZ_MODAL_DATA (the
    // `data` above) and loads itself in ngOnInit.
    return modalRef;
  }
}
