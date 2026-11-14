import { Component, computed, input, output } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { IconComponent } from '../../icons/icon.component';
import { IconName } from '../../icons/app-icons';

export type ButtonSize = 'sm' | 'md' | 'lg' | 'xl';

export type ButtonHierarchy =
  | 'primary'
  | 'soft'
  | 'link-color'
  | 'link-gray'
  | 'primary-ghost'
  | 'gray-ghost'
  | 'gray-outline';

/**
 * 'trailing' (the default) places the icon after the label in DOM order, so
 * flexbox's direction-aware main axis puts it on the right in LTR and the
 * left in RTL with no extra dir-handling - matching the Figma spec's rule
 * that the icon sits on the right in English and the left in Arabic.
 * 'leading' puts it before the label (still direction-aware); 'only' renders
 * the icon alone in a square button with no label.
 */
export type ButtonIconPosition = 'leading' | 'trailing' | 'only';

const ICON_SIZE_BY_BUTTON_SIZE: Record<ButtonSize, number> = {
  sm: 20,
  md: 20,
  lg: 22,
  xl: 22
};

@Component({
  selector: 'app-button',
  imports: [IconComponent, NzButtonModule],
  templateUrl: './button.component.html',
  styleUrls: ['./button.component.css'],
  host: {
    /** Stretches the host element itself so `block` fills its parent - see the
     * :host(.app-button-host--block) rule in button.component.css. Needed
     * because the host no longer uses `display: contents` (that broke
     * nz-tooltip/nz-dropdown positioning, which measure the host's own
     * getBoundingClientRect - a display:contents element reports a zero-size
     * box), so the inner button's `width: 100%` alone isn't enough. */
    '[class.app-button-host--block]': 'block()'
  }
})
export class ButtonComponent {
  readonly size = input<ButtonSize>('md');
  readonly hierarchy = input<ButtonHierarchy>('primary');
  readonly destructive = input(false);
  readonly disabled = input(false);
  readonly type = input<'button' | 'submit' | 'reset'>('button');

  readonly icon = input<IconName>();
  readonly iconPosition = input<ButtonIconPosition>('trailing');
  /** Extra class(es) forwarded to the icon's own <svg> (e.g. an existing
   * global class that mirrors/rotates it), on top of the sizing app-button
   * already applies via iconSize. */
  readonly iconClass = input('');
  /** Uses NG-Zorro's spinner and blocks interaction until loading is false. */
  readonly loading = input(false);
  readonly loadingLabel = input<string>();
  /** Full-width button (not a Figma variant - a layout utility for forms). */
  readonly block = input(false);

  readonly ariaLabel = input<string>();

  readonly clicked = output<MouseEvent>();

  readonly iconSize = computed(() => ICON_SIZE_BY_BUTTON_SIZE[this.size()]);

  readonly showLabel = computed(() => this.iconPosition() !== 'only');

  onClick(event: MouseEvent): void {
    this.clicked.emit(event);
  }
}
