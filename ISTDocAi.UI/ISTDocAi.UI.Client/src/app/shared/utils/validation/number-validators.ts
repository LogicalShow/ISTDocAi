// number-validators.ts
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export class NumberValidators {
  /**
   * Generic exclusive minimum: value must be > min
   */
  static minExclusive(min: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const raw = control.value;

      // allow empty values here (use Validators.required separately)
      if (raw === null || raw === undefined || raw === '') return null;

      // coerce + validate number
      const value = typeof raw === 'number' ? raw : Number(String(raw).trim());
      if (Number.isNaN(value)) return { number: true };

      return value > min ? null : { minExclusive: { min, actual: value } };
    };
  }

  /**
   * Positive number (> 0), a specialization of minExclusive
   */
  static positive(): ValidatorFn {
    return NumberValidators.minExclusive(0);
  }

  /**
   * Non-negative (>= 0) if you ever need it
   */
  static minInclusive(min: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const raw = control.value;
      if (raw === null || raw === undefined || raw === '') return null;

      const value = typeof raw === 'number' ? raw : Number(String(raw).trim());
      if (Number.isNaN(value)) return { number: true };

      return value >= min ? null : { minInclusive: { min, actual: value } };
    };
  }

  /**
   * Integer validator: value must be a whole number (no decimals)
   */
  static integer(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const raw = control.value;

      // allow empty values (use Validators.required separately)
      if (raw === null || raw === undefined || raw === '') return null;

      // coerce + validate number
      const value = typeof raw === 'number' ? raw : Number(String(raw).trim());
      if (Number.isNaN(value)) return { number: true };

      // check if it's an integer
      return Number.isInteger(value) ? null : { integer: { actual: value } };
    };
  }

  /**
   * Positive integer validator: value must be > 0 and a whole number
   * Combines positive() and integer() validators
   */
  static positiveInteger(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const raw = control.value;

      // allow empty values (use Validators.required separately)
      if (raw === null || raw === undefined || raw === '') return null;

      // coerce + validate number
      const value = typeof raw === 'number' ? raw : Number(String(raw).trim());
      if (Number.isNaN(value)) return { number: true };

      // check if integer
      if (!Number.isInteger(value)) return { integer: { actual: value } };

      // check if positive
      return value > 0 ? null : { minExclusive: { min: 0, actual: value } };
    };
  }

  /**
   * Keyboard event handler to prevent non-numeric input
   * Use with (keydown) event on numeric input fields
   * 
   * @example
   * <input (keydown)="NumberValidators.onlyNumericInput($event)" />
   * 
   * @param event - The keyboard event
   */
  static onlyNumericInput(event: KeyboardEvent): void {
    // Allow: backspace, delete, tab, escape, enter, home, end, arrow keys
    const allowedKeys = ['Backspace', 'Delete', 'Tab', 'Escape', 'Enter', 'Home', 'End', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'];
    
    // Allow Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X (and Cmd on Mac)
    if ((event.ctrlKey || event.metaKey) && ['a', 'c', 'v', 'x'].includes(event.key.toLowerCase())) {
      return;
    }
    
    // If key is in allowed keys, allow it
    if (allowedKeys.includes(event.key)) {
      return;
    }
    
    // Only allow numeric keys (0-9)
    if (!/^\d$/.test(event.key)) {
      event.preventDefault();
    }
  }
}
