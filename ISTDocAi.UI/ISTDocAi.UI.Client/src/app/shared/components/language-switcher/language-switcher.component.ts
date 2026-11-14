import { Component, inject, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { Culture } from '../../../core/constants/culture.constants';
import { TranslationService } from '../../../core/services/translation.service';
import { IconName } from '../../icons/app-icons';
import { IconComponent } from '../../icons/icon.component';

export type LanguageSwitcherAppearance = 'auth' | 'forgot' | 'header';

@Component({
  selector: 'app-language-switcher',
  templateUrl: './language-switcher.component.html',
  styleUrl: './language-switcher.component.css',
  imports: [NzDropdownModule, NzMenuModule, TranslatePipe, IconComponent]
})
export class LanguageSwitcherComponent {
  private readonly translationService = inject(TranslationService);

  readonly appearance = input<LanguageSwitcherAppearance>('auth');
  readonly globeIcon = input<IconName>('globe-light');
  readonly languageChanged = output<Culture>();

  readonly languages: readonly { code: Culture; label: string }[] = [
    { code: 'en', label: 'English' },
    { code: 'ar', label: 'العربية' }
  ];

  readonly currentCulture = this.translationService.culture;

  async switchLanguage(culture: Culture): Promise<void> {
    if (culture === this.currentCulture()) {
      return;
    }

    await this.translationService.use(culture);
    this.languageChanged.emit(culture);
  }
}
