import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { NzNotificationService } from 'ng-zorro-antd/notification';
import { NzModalService, NzModalRef } from 'ng-zorro-antd/modal';
import { take } from 'rxjs';
import { FEEDBACK_DEFAULTS } from '../constants/feedback.constants';
import { ApiErrorType, ApiResultFailure } from '../models/api-error.model';
import { extractApiFailure } from '../../shared/utils/api-result.utils';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  ConfirmOptions,
  FeedbackOptions,
  ToastOptions
} from '../models/feedback.model';

type NotificationType = 'success' | 'info' | 'warning' | 'error';

/** Result is `true` when confirmed, `undefined` when cancelled/dismissed. */
export type ConfirmModalRef = NzModalRef<ConfirmDialogComponent, boolean | undefined>;

@Injectable({
  providedIn: 'root'
})
export class FeedbackService {
  private notification = inject(NzNotificationService);
  private modal = inject(NzModalService);
  private translate = inject(TranslateService);

  private readonly notificationOptions = {
    nzPlacement: 'bottomLeft' as const,
    nzClass: 'feedback-notification',
    nzDuration: FEEDBACK_DEFAULTS.durationMs
  };

  success(titleKey: string, messageKey: string, options?: FeedbackOptions): void {
    this.showTranslated('success', titleKey, messageKey, options);
  }

  info(titleKey: string, messageKey: string, options?: FeedbackOptions): void {
    this.showTranslated('info', titleKey, messageKey, options);
  }

  apiInfo(title: string, message: string, options?: ToastOptions): void {
    this.show('info', title, message, options);
  }

  warning(titleKey: string, messageKey: string, options?: FeedbackOptions): void {
    this.showTranslated('warning', titleKey, messageKey, options);
  }

  error(titleKey: string, messageKey: string, options?: FeedbackOptions): void {
    this.showTranslated('error', titleKey, messageKey, options);
  }

  handleApiError(source: unknown, options?: ToastOptions): void {
    const failure = extractApiFailure(source);

    if (failure?.Error.Code === 'Concurrency') {
      this.concurrencyConflict();
      return;
    }

    this.defaultApiError(failure, options);
  }

  private defaultApiError(failure: ApiResultFailure | null, options?: ToastOptions): void {
    if (!failure) {
      this.error(
        'feedback.system.unexpected.title',
        'feedback.system.unexpected.message',
        options
      );
      return;
    }

    const validationErrors = failure.Errors?.length
      ? failure.Errors
      : failure.ValidationErrors ?? [];
    const message = validationErrors
      .map(error => error.Message)
      .filter(Boolean)
      .join('\n') || failure.Error.Message;
    const title = failure.Error.Type === ApiErrorType.Validation
      ? this.translate.instant(FEEDBACK_DEFAULTS.validation.titleKey)
      : failure.Error.Code;

    this.show('error', title, message, options);
  }

  private concurrencyConflict(): void {
    this.confirm(
      'feedback.system.concurrency.title',
      'feedback.system.concurrency.message',
      () => {
        window.location.reload();
        return true;
      },
      { okLabelKey: 'common.reload' }
    );
  }

  /** Fire-and-forget confirmation: the callback runs on confirm. */
  confirm(
    titleKey: string,
    messageKey: string,
    okResponse: () => boolean,
    options?: ConfirmOptions
  ): void;
  /** Awaitable confirmation: afterClose emits true on confirm, undefined otherwise. */
  confirm(titleKey: string, messageKey: string, options?: ConfirmOptions): ConfirmModalRef;

  confirm(
    titleKey: string,
    messageKey: string,
    okResponseOrOptions?: (() => boolean) | ConfirmOptions,
    options?: ConfirmOptions
  ): ConfirmModalRef | void {
    const okResponse = typeof okResponseOrOptions === 'function' ? okResponseOrOptions : undefined;
    const confirmOptions = typeof okResponseOrOptions === 'function' ? options : okResponseOrOptions;

    const data: ConfirmDialogData = {
      title: this.translate.instant(titleKey),
      message: this.translate.instant(messageKey, confirmOptions?.params),
      confirmLabel: this.translate.instant(confirmOptions?.okLabelKey ?? 'common.confirm'),
      cancelLabel: this.translate.instant(confirmOptions?.cancelLabelKey ?? 'common.cancel'),
      destructive: confirmOptions?.destructive ?? false
    };

    const modalRef: ConfirmModalRef = this.modal.create({
      nzContent: ConfirmDialogComponent,
      nzData: data,
      nzClassName: 'app-confirm-modal',
      nzWidth: 485,
      nzCentered: true,
      nzClosable: false,
      nzFooter: null,
      nzMaskClosable: false
    });

    if (okResponse) {
      modalRef.afterClose.pipe(take(1)).subscribe(result => {
        if (result === true) {
          okResponse();
        }
      });
      return;
    }

    return modalRef;
  }

  private showTranslated(
    type: NotificationType,
    titleKey: string,
    messageKey: string,
    options?: FeedbackOptions
  ): void {
    this.show(
      type,
      this.translate.instant(titleKey),
      this.translate.instant(messageKey, options?.messageParams),
      options
    );
  }

  private show(
    type: NotificationType,
    title: string,
    message: string,
    options?: ToastOptions
  ): void {
    const notificationOptions = {
      ...this.notificationOptions,
      nzDuration: this.resolveDurationMs(options?.durationMs)
    };

    this.notification.create(
      type,
      title,
      message,
      notificationOptions
    );
  }

  private resolveDurationMs(durationMs?: number): number {
    return durationMs !== undefined && Number.isFinite(durationMs) && durationMs >= 0
      ? durationMs
      : FEEDBACK_DEFAULTS.durationMs;
  }

}
