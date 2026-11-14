import { Injectable, signal, inject } from '@angular/core';
import { Directionality } from '@angular/cdk/bidi';
import { firstValueFrom } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { NzI18nService, ar_EG, en_US } from 'ng-zorro-antd/i18n';
import { AppStore } from '../state/app.store';
import { CULTURE_STORAGE_KEY, Culture, DEFAULT_CULTURE, SUPPORTED_CULTURES, isSupportedCulture } from '../constants/culture.constants';

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  private translate = inject(TranslateService);
  private nzI18n = inject(NzI18nService);
  private appStore = inject(AppStore);
  private directionality = inject(Directionality);

  private readonly cultureState = signal<Culture>(DEFAULT_CULTURE);
  readonly culture = this.cultureState.asReadonly();

  constructor() {
    this.translate.addLangs([...SUPPORTED_CULTURES]);
    this.translate.setFallbackLang(DEFAULT_CULTURE);
  }

  async init(): Promise<void> {
    const storedCulture = localStorage.getItem(CULTURE_STORAGE_KEY);
    await this.use(isSupportedCulture(storedCulture) ? storedCulture : DEFAULT_CULTURE);
  }

  async use(culture: Culture): Promise<void> {
    localStorage.setItem(CULTURE_STORAGE_KEY, culture);
    this.cultureState.set(culture);

    await firstValueFrom(this.translate.use(culture));

    this.nzI18n.setLocale(culture === 'ar' ? ar_EG : en_US);
    document.documentElement.lang = culture;
    const dir = culture === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.dir = dir;

    // Angular CDK's Directionality service only reads document.dir once, at
    // construction, and every ng-zorro component (menu, submenu, dropdown,
    // popover) caches that value in ngOnInit and only ever updates it again
    // by subscribing to `change`. Without emitting here, switching language
    // at runtime leaves already-rendered menus/overlays (e.g. the sidebar and
    // the header's profile/language popovers) stuck mirrored to whichever
    // direction was active when they first rendered.
    // `value` is a getter over `valueSignal` (assigning it throws), so the
    // app-wide singleton is updated through the writable signal instead.
    this.directionality.valueSignal.set(dir);
    this.directionality.change.emit(dir);

    this.appStore.updateHostInfo({ uiLanguage: culture });
  }
}
