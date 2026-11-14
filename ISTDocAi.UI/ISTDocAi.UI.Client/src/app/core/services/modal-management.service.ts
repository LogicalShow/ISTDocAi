import { Injectable, Type, inject } from '@angular/core';
import { NzModalService, NzModalRef } from 'ng-zorro-antd/modal';
import { Observable, firstValueFrom, map } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import {
  EntityModalConfig,
  ModalConfig,
  SaveableAndNewComponent,
  SaveableComponent,
  SearchableComponent
} from '../models/modal-config.model';
import { FeedbackService } from './feedback.service';
import { HasUnsavedChanges } from '../models/has-unsaved-changes.model';

@Injectable({
  providedIn: 'root'
})
export class ModalManagementService {
  private modal = inject(NzModalService);
  private translate = inject(TranslateService);
  private feedbackService = inject(FeedbackService);

  openModal<T>(config: ModalConfig<T>, mode: string): NzModalRef<T> {
    const isSearchMode = mode === 'search';
    const isViewMode = mode === 'view' || this.hasViewMode(config.data);

    const modalRef: NzModalRef<T> = this.modal.create({
      nzTitle: this.translate.instant(config.title),
      nzContent: config.component,
      nzData: config.data,
      nzWidth: config.width || 900,
      nzClassName: config.nzClassName,
      // Gates the X icon, mask click, and Escape (all three route through
      // nzOnCancel) behind the same unsaved-changes check as the footer
      // Close button below, so none of them can bypass it.
      nzOnCancel: () => this.confirmDiscard(modalRef.getContentComponent()).then(proceed => proceed ? undefined : false),
      nzFooter: config.nzFooter === false
        ? []
        : (isViewMode
          ? [
            {
              label: this.translate.instant('modal.actions.cancel'),
              onClick: () => this.closeWithConfirmation(modalRef, config)
            }
          ]
          : mode === 'add'
            ? [
              {
                label: this.translate.instant('modal.actions.close'),
                onClick: () => this.closeWithConfirmation(modalRef, config)
              },
              {
                // unchanged for ADD
                label: this.translate.instant('modal.actions.save'),
                type: 'primary',
                onClick: () => {
                  if (config.onSave) {
                    config.onSave();
                  } else {
                    const componentInstance = modalRef.getContentComponent() as SaveableComponent;
                    if (componentInstance && typeof componentInstance.save === 'function') {
                      componentInstance.save();
                    }
                  }
                }
              },
              {
                // unchanged for ADD
                label: this.translate.instant('modal.actions.saveAndAddAnother'),
                type: 'primary',
                onClick: () => {
                  if (config.onSaveAndNew) {
                    config.onSaveAndNew();
                  } else {
                    const componentInstance = modalRef.getContentComponent() as SaveableAndNewComponent;
                    if (componentInstance && typeof componentInstance.saveAndNew === 'function') {
                      componentInstance.saveAndNew();
                    }
                  }
                }
              }
            ]
            : [
              {
                label: this.translate.instant('modal.actions.close'),
                onClick: () => this.closeWithConfirmation(modalRef, config)
              },
              {
                // ONLY this button changes when mode === 'search'
                label: this.translate.instant(isSearchMode ? 'modal.actions.search' : 'modal.actions.save'),
                type: 'primary',
                onClick: () => {
                  if (isSearchMode) {
                    // Prefer explicit handler if provided
                    if (config.onSearch) {
                      config.onSearch();
                      return;
                    }
                    // Otherwise call a search-like method on the component if it exists
                    const cmp = modalRef.getContentComponent() as SearchableComponent;
                    if (cmp) {
                      if (typeof cmp.search === 'function') return cmp.search();
                      if (typeof cmp.onSearch === 'function') return cmp.onSearch();
                    }
                    // Final fallback: keep old behavior (save) to avoid breaking flows
                    if (config.onSave) {
                      config.onSave();
                    } else {
                      const componentInstance = modalRef.getContentComponent() as SaveableComponent;
                      if (componentInstance && typeof componentInstance.save === 'function') {
                        componentInstance.save();
                      }
                    }
                  } else {
                    // original SAVE behavior for all other modes
                    if (config.onSave) {
                      config.onSave();
                    } else {
                      const componentInstance = modalRef.getContentComponent() as SaveableComponent;
                      if (componentInstance && typeof componentInstance.save === 'function') {
                        componentInstance.save();
                      }
                    }
                  }
                }
              }
            ])
    });

    return modalRef;
  }


  openEntityModal<T, TEntity extends object = object>(
    config: EntityModalConfig<T, TEntity>,
    onSuccess?: () => void
  ): NzModalRef<T> {
    const isViewMode = config.isViewMode ?? config.mode === 'view';

    const modalRef = this.openModal(
      {
        title: this.getEntityModalTitle(config, isViewMode),
        component: config.component,
        data: {
          mode: config.mode,
          entity: config.entity,
          isViewMode: isViewMode,
          nzFooter: config.nzFooter,
        },
        width: config.width,
        onSave: config.onSave,
        onCancel: config.onCancel,
        onSaveAndNew: config.mode === 'add' ? config.onSaveAndNew : undefined,
        nzFooter: config.nzFooter,
        nzClassName: config.nzClassName,
      },
      config.mode
    );

    if (onSuccess) {
      modalRef.afterClose.subscribe(result => {
        if (result === 'success') {
          onSuccess();
        }
      });
    }

    return modalRef;
  }

  openAddModal<T, TEntity extends object = object>(
    title: string,
    component: Type<T>,
    onSuccess?: () => void,
    data?: TEntity,
    width?: string | number,
    nzClassName?: string
  ): NzModalRef<T> {
    return this.openEntityModal({
      title,
      component,
      mode: 'add',
      entity: data,
      width,
      nzClassName
    }, onSuccess);
  }

  openEditModal<T, TEntity extends object>(
    title: string,
    component: Type<T>,
    entityId: string,
    fetchEntityFn: (id: string) => Observable<TEntity>,
    onSuccess?: () => void,
    onError?: (error: Error) => void,
    width?: string | number
  ): void {
    fetchEntityFn(entityId).subscribe({
      next: persistedEntity => {
        if (!persistedEntity) {
          console.error('Entity not found');
          if (onError) onError(new Error('Entity not found'));
          return;
        }

        this.openEntityModal({
          title,
          component,
          mode: 'edit',
          entity: persistedEntity,
          width
        }, onSuccess);
      },
      error: (error) => {
        console.error('Failed to retrieve entity data:', error);
        if (onError) onError(error instanceof Error ? error : new Error('Failed to retrieve entity data'));
      }
    });
  }

  openSearchModal<T, TCriteria extends object>(
    title: string,
    component: Type<T>,
    criteria: TCriteria,
    nzClassName?: string
  ): NzModalRef<T> {
    return this.openModal({
      title,
      component,
      data: criteria,
      width: 600,
      nzClassName
    }, 'search');
  }

  private getEntityModalTitle<T>(config: EntityModalConfig<T>, isViewMode: boolean): string {
    const title = this.translate.instant(config.title);
    if (isViewMode || config.nzFooter === false) {
      return this.translate.instant('modal.title.view', { title });
    }
    if (config.mode === 'add') {
      return this.translate.instant('modal.title.add', { title });
    }
    return this.translate.instant('modal.title.edit', { title });
  }

  private hasViewMode(data?: object): boolean {
    return data != null && 'isViewMode' in data && data.isViewMode === true;
  }

  /** Runs a content component's own close handler, but only after confirming
   * discard of any unsaved changes (same gate as nzOnCancel above). */
  private closeWithConfirmation<T>(modalRef: NzModalRef<T>, config: ModalConfig<T>): void {
    this.confirmDiscard(modalRef.getContentComponent()).then(proceed => {
      if (!proceed) {
        return;
      }
      if (config.onCancel) {
        config.onCancel();
      }
      modalRef.close();
    });
  }

  /** Resolves true when it's safe to close: the content component either
   * doesn't report unsaved changes, or the user confirmed discarding them.
   * Mirrors unsavedChangesGuard's duck-typed HasUnsavedChanges check, applied
   * to modal close instead of router navigation. */
  private confirmDiscard(instance: unknown): Promise<boolean> {
    const target = instance as Partial<HasUnsavedChanges> | null;
    if (typeof target?.hasUnsavedChanges !== 'function' || !target.hasUnsavedChanges()) {
      return Promise.resolve(true);
    }

    return firstValueFrom(
      this.feedbackService
        .confirm(
          'feedback.navigation.unsavedChanges.title',
          'feedback.navigation.unsavedChanges.message',
          { okLabelKey: 'common.discard', destructive: true }
        )
        .afterClose.pipe(map(result => result === true))
    );
  }
}
