import { Injector, Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, ControlEvent } from '@angular/forms';

/**
 * Signal that changes on every value, status, touched or pristine event of the
 * control or any of its descendants.
 *
 * Components are OnPush, so a template helper that reads form state (an error
 * message, `form.invalid`, a masked-value check) only re-renders when a signal
 * it read changes. Form state is often changed outside the template - a modal
 * footer calling save() -> markAsTouched(), or patchValue() in an HTTP callback -
 * so those helpers read this signal first to pick the change up.
 *
 * Pass `injector` when the form is created outside an injection context
 * (e.g. in ngOnInit).
 */
export function formChanges(control: AbstractControl, injector?: Injector): Signal<ControlEvent | null> {
  return toSignal(control.events, { initialValue: null, injector });
}
