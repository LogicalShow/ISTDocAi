import { Component, computed, input, model, output } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { TranslatePipe } from '@ngx-translate/core';
import { IconComponent } from '../../icons/icon.component';

@Component({
  selector: 'app-search-toolbar',
  imports: [FormsModule, NzInputModule, NzButtonModule, IconComponent, TranslatePipe],
  templateUrl: './search-toolbar.component.html',
  styleUrls: ['./search-toolbar.component.scss']
})
export class SearchToolbarComponent {
  /** Input placeholder text */
  readonly placeholder = input('searchToolbar.placeholder');

  /** Two-way bound search value */
  readonly value = model('');

  /** Fires when user triggers a search (Enter or icon click) */
  readonly searchTriggered = output<string>();

  /** Fires when “Show Filters” clicked */
  readonly openFilters = output<void>();

  /** Fires when "Clear" clicked (after value is cleared) */
  readonly clear = output<void>();

  /** Hide the search icon and action buttons (Show Filters, Clear) */
  readonly hideActions = input(false);

  /** Width of the quick-search area. Number = px; or pass any CSS length (e.g., '24rem', '50%'). */
  readonly width = input<number | string>(500);

  readonly computedWidth = computed(() => {
    const width = this.width();
    return typeof width === 'number' ? `${width}px` : width;
  });

  onInput(val: string) {
    this.value.set(val);
  }

  onSearch() {
    this.searchTriggered.emit(this.value()?.trim());
  }

  onOpenFilters() {
    this.openFilters.emit();
  }

  onClear() {
    this.value.set('');
    this.clear.emit();
  }
}
