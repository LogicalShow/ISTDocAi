import { Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ThemeService } from '../../../core/services/theme.service';
import { IconName } from '../../icons/app-icons';
import { IconComponent } from '../../icons/icon.component';

export type ThemeSwitcherAppearance = 'auth' | 'forgot' | 'header';

@Component({
  selector: 'app-theme-switcher',
  templateUrl: './theme-switcher.component.html',
  styleUrl: './theme-switcher.component.css',
  imports: [IconComponent, TranslatePipe]
})
export class ThemeSwitcherComponent {
  private readonly themeService = inject(ThemeService);

  readonly appearance = input<ThemeSwitcherAppearance>('header');
  readonly theme = this.themeService.theme;
  readonly icon = computed<IconName>(() => this.theme() === 'dark' ? 'sun-light' : 'moon-light');
  readonly labelKey = computed(() =>
    this.theme() === 'dark' ? 'common.switchToLightMode' : 'common.switchToDarkMode'
  );

  toggleTheme(): void {
    this.themeService.toggle();
  }
}
