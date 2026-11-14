import { Component, computed, forwardRef, input, output, signal } from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR, ControlValueAccessor } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import type { NzSelectModeType, NzSelectOptionInterface, NzSelectPlacementType } from 'ng-zorro-antd/select';

/**
 * Thin styling wrapper around nz-select that stamps the shared Figma
 * "Dropdown Menu" panel skin (see styles/vendor/dropdown-panel.css) onto
 * every select's dropdown. Forwards the ng-zorro inputs/outputs actually
 * used across the app rather than nz-select's full surface, and implements
 * ControlValueAccessor so it drops into existing `formControlName` /
 * `[(ngModel)]` call sites unchanged.
 *
 * Options are passed as data (`[options]`) via nz-select's own `nzOptions`
 * input rather than content-projected `<nz-option>` children: nz-select
 * populates its list from `@ContentChildren(NzOptionComponent)`, and that
 * query never sees options projected through this wrapper's own
 * `<ng-content>` - confirmed empirically (against the real backend) as an
 * Angular content-projection limitation, not a nz-select config issue - so
 * content projection can silently render "No Data" even with real rows.
 */
@Component({
  selector: 'app-select',
  imports: [FormsModule, NzSelectModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SelectComponent),
      multi: true
    }
  ],
  templateUrl: './select.component.html',
  styleUrls: ['./select.component.css']
})
export class SelectComponent implements ControlValueAccessor {
  readonly id = input<string>();
  readonly options = input<NzSelectOptionInterface[]>([]);
  readonly placeholder = input<string | null>(null);
  readonly allowClear = input(false);
  readonly borderless = input(false);
  readonly showSearch = input(false);
  readonly serverSearch = input(false);
  readonly loading = input(false);
  readonly disabled = input(false);
  readonly mode = input<NzSelectModeType>('default');
  readonly placement = input<NzSelectPlacementType | null>(null);

  readonly searchChange = output<string>();
  readonly openChange = output<boolean>();

  // writeValue/setDisabledState are called by the forms API from outside this
  // component's template, so both land in signals to refresh the view.
  readonly value = signal<unknown>(null);
  private readonly formDisabled = signal(false);
  readonly isDisabled = computed(() => this.disabled() || this.formDisabled());

  private onChange: (value: unknown) => void = () => undefined;
  onTouched: () => void = () => undefined;

  onValueChange(value: unknown): void {
    this.value.set(value);
    this.onChange(value);
  }

  writeValue(value: unknown): void {
    this.value.set(value);
  }

  registerOnChange(fn: (value: unknown) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.formDisabled.set(isDisabled);
  }
}
