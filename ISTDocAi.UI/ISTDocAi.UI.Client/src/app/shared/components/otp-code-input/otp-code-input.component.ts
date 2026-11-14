import { AfterViewInit, Component, ElementRef, QueryList, ViewChildren, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';

const OTP_LENGTH = 6;

/**
 * Six single-digit boxes for a one-time code, exposed as one form control
 * holding the joined string (so `Validators.pattern(/^\d{6}$/)` works on the
 * parent). Typing advances focus, Backspace on an empty box retreats, and a
 * pasted code is spread across the boxes. The row is pinned to LTR so digits
 * read left-to-right in Arabic too. Used by forgot-password and the
 * change-password modal.
 */
@Component({
  selector: 'app-otp-code-input',
  imports: [TranslatePipe],
  templateUrl: './otp-code-input.component.html',
  styleUrl: './otp-code-input.component.css',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => OtpCodeInputComponent), multi: true }]
})
export class OtpCodeInputComponent implements ControlValueAccessor, AfterViewInit {
  @ViewChildren('otpInput') private inputs?: QueryList<ElementRef<HTMLInputElement>>;

  /** Error line shown under the boxes; null hides it. The parent decides when (touched && invalid). */
  readonly error = input<string | null>(null);

  readonly digits = signal<string[]>(Array(OTP_LENGTH).fill(''));
  readonly disabled = signal(false);

  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  ngAfterViewInit(): void {
    // The control only appears once a code has been sent, so land the caret
    // in the first box straight away.
    this.focus();
  }

  writeValue(value: string | null): void {
    const clean = (value ?? '').replace(/\D/g, '').slice(0, OTP_LENGTH);
    this.digits.set(Array.from({ length: OTP_LENGTH }, (_, index) => clean[index] ?? ''));
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  focus(): void {
    this.inputs?.first?.nativeElement.focus();
  }

  updateDigit(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const digit = input.value.replace(/\D/g, '').slice(-1);
    // Force the native value back to the sanitized digit: if a non-numeric
    // keystroke resulted in the same (unchanged) digit, Angular's binding
    // won't touch the DOM, leaving the typed character visible.
    input.value = digit;
    const digits = [...this.digits()];
    digits[index] = digit;
    this.digits.set(digits);
    this.emit();

    if (digit && index < OTP_LENGTH - 1) {
      this.inputs?.get(index + 1)?.nativeElement.focus();
    }
  }

  handleKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.digits()[index] && index > 0) {
      this.inputs?.get(index - 1)?.nativeElement.focus();
    }

    const isControlKey = event.key.length > 1 || event.ctrlKey || event.metaKey;
    if (!isControlKey && !/^\d$/.test(event.key)) {
      event.preventDefault();
    }
  }

  paste(event: ClipboardEvent): void {
    const pasted = event.clipboardData?.getData('text').replace(/\D/g, '').slice(0, OTP_LENGTH) ?? '';
    if (!pasted) {
      return;
    }

    event.preventDefault();
    this.digits.set(Array.from({ length: OTP_LENGTH }, (_, index) => pasted[index] ?? ''));
    this.emit();
    this.inputs?.get(Math.min(pasted.length, OTP_LENGTH) - 1)?.nativeElement.focus();
  }

  handleBlur(): void {
    this.onTouched();
  }

  private emit(): void {
    this.onChange(this.digits().join(''));
  }
}
