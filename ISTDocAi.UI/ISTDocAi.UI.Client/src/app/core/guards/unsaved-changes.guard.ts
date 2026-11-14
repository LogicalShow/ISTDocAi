import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { Observable, map } from 'rxjs';
import { FeedbackService } from '../services/feedback.service';
import { HasUnsavedChanges } from '../models/has-unsaved-changes.model';

/**
 * Blocks navigation away from a component that reports unsaved changes.
 *
 * withUnsavedChangesGuard applies this to every routed component, so the check
 * is duck-typed: components that do not implement HasUnsavedChanges pass through.
 */
export const unsavedChangesGuard: CanDeactivateFn<unknown> = (
  component
): boolean | Observable<boolean> => {
  const target = component as Partial<HasUnsavedChanges> | null;

  if (typeof target?.hasUnsavedChanges !== 'function' || !target.hasUnsavedChanges()) {
    return true;
  }

  return inject(FeedbackService)
    .confirm(
      'feedback.navigation.unsavedChanges.title',
      'feedback.navigation.unsavedChanges.message',
      { okLabelKey: 'common.discard', destructive: true }
    )
    .afterClose
    .pipe(map(result => result === true));
};
