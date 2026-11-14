// required-marker.directive.ts
import { DestroyRef, Directive, ElementRef, OnInit, OnDestroy, Renderer2, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgControl, FormArrayName } from '@angular/forms';

/**
 * Automatically adds a red asterisk (*) to labels of required form fields.
 * Reacts to dynamic validator changes and enable/disable states.
 * 
 * @example
 * // Add the directive to a control with Validators.required
 * <div class="form-item">
 *   <label>Email</label>
 *   <input appRequiredMarker formControlName="email" />
 * </div>
 */
@Directive({
  selector: '[appRequiredMarker]'
})
export class RequiredMarkerDirective implements OnInit, OnDestroy {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly renderer = inject(Renderer2);
  private readonly ngControl = inject(NgControl, { optional: true, self: true });
  private readonly formArrayName = inject(FormArrayName, { optional: true, self: true });
  private readonly destroyRef = inject(DestroyRef);
  private asteriskElement: HTMLElement | null = null;

  // Configuration
  private readonly FORM_CONTAINERS = '.form-field, .form-item, .form-group, mat-form-field, nz-form-item';
  private readonly TABLE_CELLS = 'td, th';
  private readonly ASTERISK_CLASS = 'required-asterisk';

  // Validators that indicate a field is truly required (not just format validators)
  // These validators mean "you MUST provide a value"
  private readonly REQUIRED_VALIDATORS = [
    'required',      // Validators.required
    'minSelection',  // Custom minSelection(n) - must select at least n items
  ];

  ngOnInit(): void {
    const control = this.getControl();
    if (!control) return;

    /*
    // Debug logging 
    const elementInfo = this.el.nativeElement.tagName +
      (this.el.nativeElement.getAttribute('formControlName')
        ? `[formControlName="${this.el.nativeElement.getAttribute('formControlName')}"]`
        : this.el.nativeElement.getAttribute('formArrayName')
          ? `[formArrayName="${this.el.nativeElement.getAttribute('formArrayName')}"]`
          : '');
    console.log('RequiredMarker attached to:', elementInfo);

    */

    this.updateAsterisk();

    // React to validator and state changes
    control.statusChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.updateAsterisk());
  }

  ngOnDestroy(): void {
    this.removeAsterisk();
  }

  /**
   * Get the control from either NgControl or FormArrayName
   */
  private getControl() {
    return this.ngControl?.control ?? this.formArrayName?.control;
  }

  /**
   * Main logic: Show asterisk only if field is required AND enabled
   */
  private updateAsterisk(): void {
    const shouldShow = this.isRequired() && !this.isDisabled();

    if (shouldShow && !this.asteriskElement) {
      this.addAsterisk();
    } else if (!shouldShow && this.asteriskElement) {
      this.removeAsterisk();
    }
  }

  /**
   * Check if control has Validators.required or custom required-like validators
   */
  private isRequired(): boolean {
    const control = this.getControl();
    if (!control?.validator) return false;

    const validationResult = control.validator(control);
    if (!validationResult) return false;

    // Check if any of the configured required validators exist
    return this.REQUIRED_VALIDATORS.some(key => validationResult[key] !== undefined);
  }

  /**
   * Check if control is disabled
   */
  private isDisabled(): boolean {
    return this.getControl()?.disabled ?? false;
  }

  /**
   * Find and attach asterisk to the appropriate label
   */
  private addAsterisk(): void {
    const label = this.findLabel();
    if (!label) return;

    this.asteriskElement = this.createAsteriskElement();
    this.renderer.appendChild(label, this.asteriskElement);
  }

  /**
   * Remove asterisk from DOM
   */
  private removeAsterisk(): void {
    if (this.asteriskElement?.parentNode) {
      this.renderer.removeChild(this.asteriskElement.parentNode, this.asteriskElement);
      this.asteriskElement = null;
    }
  }

  /**
   * Smart label detection with multiple strategies
   */
  private findLabel(): HTMLElement | null {
    // Strategy 1: Use label[for] if input has an id
    const labelByFor = this.findLabelByFor();
    if (labelByFor) return labelByFor;

    // Strategy 2: Find label in parent container (but skip table cells)
    return this.findLabelInContainer();
  }

  /**
   * Find label using for="inputId" attribute
   */
  private findLabelByFor(): HTMLElement | null {
    const inputId = this.el.nativeElement.id;
    return inputId
      ? document.querySelector(`label[for="${inputId}"]`)
      : null;
  }

  /**
   * Find label in closest form container, excluding table cells
   */
  private findLabelInContainer(): HTMLElement | null {
    const container = this.el.nativeElement.closest(this.FORM_CONTAINERS);
    if (!container) return null;

    // Skip if we're inside a table cell (table rows shouldn't get asterisks)
    if (this.isInsideTableCell()) return null;

    // Try multiple label selectors (standard label, nz-form-label, mat-label)
    return container.querySelector(':scope > label') ??
      container.querySelector('label') ??
      container.querySelector('nz-form-label') ??
      container.querySelector('mat-label');
  }

  /**
   * Check if element is inside a table cell
   */
  private isInsideTableCell(): boolean {
    return !!this.el.nativeElement.closest(this.TABLE_CELLS);
  }

  /**
   * Create styled asterisk element
   */
  private createAsteriskElement(): HTMLElement {
    const span = this.renderer.createElement('span');
    this.renderer.addClass(span, this.ASTERISK_CLASS);
    this.renderer.setStyle(span, 'color', 'red');
    this.renderer.setStyle(span, 'margin-left', '4px');

    const text = this.renderer.createText('*');
    this.renderer.appendChild(span, text);

    return span;
  }
}

// Usage Examples:
//
// 1. Simple form field:
//    <div class="form-item">
//      <label>Name</label>
//      <input formControlName="name" />
//    </div>
//
// 2. With label[for]:
//    <label for="email">Email</label>
//    <input id="email" formControlName="email" />
//
// 3. Dynamic validators:
//    this.form.get('field')?.setValidators([Validators.required]);
//    this.form.get('field')?.updateValueAndValidity();
//
// 4. Enable/Disable:
//    this.form.get('field')?.disable(); // Asterisk disappears
//    this.form.get('field')?.enable();  // Asterisk reappears
