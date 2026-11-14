/**
 * Implemented by components that hold user-entered data. Consulted by
 * unsavedChangesGuard for routed components, and by ModalManagementService
 * for components hosted in a modal.
 */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}
