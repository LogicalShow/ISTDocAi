import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Makes a control required if `predicate(parent)` returns true.
 * Useful for conditional "required" rules based on sibling controls.
 */
export function requiredIf(
  predicate: (parent: AbstractControl) => boolean,
  errorKey = 'required'
): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const parent = control.parent;
    if (!parent) return null; // control not attached yet

    const empty =
      control.value == null || String(control.value).trim().length === 0;

    if (predicate(parent) && empty) {
      return { [errorKey]: true }; // triggers Angular's "required" error
    }
    return null;
  };
}

/**
 * Shortcut: makes a control required if a sibling control equals `expected`.
 */
export function requiredIfEquals(
  siblingKey: string,
  expected: string | number | boolean | null | undefined,
  errorKey = 'required'
): ValidatorFn {
  return requiredIf(parent => {
    const val = parent.get(siblingKey)?.value;
    return val === expected || Number(val) === Number(expected);
  }, errorKey);
}
