import { Type } from '@angular/core';

export interface ModalConfig<T = object> {
  title: string;
  component: Type<T>;
  data?: object;
  width?: number | string;
  onSave?: () => void;
  onCancel?: () => void;
  onSaveAndNew?: () => void;
  nzFooter?: boolean;
  nzClassName?: string;
  onSearch?: () => void;
}
export interface EntityModalConfig<T = object, TEntity extends object = object> extends ModalConfig<T> {
  mode: 'view' | 'add' | 'edit';
  entity?: TEntity;
  isViewMode?: boolean;
}
export interface SaveableComponent {
  save(): void;
}

export interface SaveableAndNewComponent {
  saveAndNew(): void;
}

export interface SearchableComponent {
  search?(): void;
  onSearch?(): void;
}
