import { Component, computed, input } from '@angular/core';
import { IconName } from './app-icons';
import { ICON_SIZES } from './icon-sizes';

const SPRITE_PATH = 'assets/icons/app-icons.svg';

@Component({
  selector: 'app-icon',
  host: { style: 'display: contents;' },
  template: `
    <svg
      [class]="iconClass()"
      [attr.width]="width() ?? defaultSize().width"
      [attr.height]="height() ?? defaultSize().height"
      fill="none"
      [style.color]="resolvedColor()"
      [attr.aria-label]="ariaLabel()"
      [attr.aria-hidden]="ariaLabel() ? null : 'true'"
      [attr.role]="ariaLabel() ? 'img' : null"
      xmlns="http://www.w3.org/2000/svg"
    >
      <use [attr.href]="spriteHref()" />
    </svg>
  `
})
export class IconComponent {
  readonly name = input.required<IconName>();
  readonly width = input<number>();
  readonly height = input<number>();
  readonly color = input<string>();
  readonly fill = input('currentColor');
  readonly iconClass = input('');
  readonly ariaLabel = input<string>();

  readonly spriteHref = computed(() => `${SPRITE_PATH}#${this.name()}`);

  readonly defaultSize = computed(() => ICON_SIZES[this.name()]);

  readonly resolvedColor = computed(() => {
    const fill = this.fill();
    if (fill && fill !== 'currentColor') {
      return fill;
    }
    return this.color();
  });
}
