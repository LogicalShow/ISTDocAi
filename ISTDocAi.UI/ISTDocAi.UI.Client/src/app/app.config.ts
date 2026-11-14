import { registerLocaleData } from '@angular/common';
import ar from '@angular/common/locales/ar';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection, importProvidersFrom, provideAppInitializer, inject, isDevMode } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { CookieService } from 'ngx-cookie-service';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NZ_I18N, ar_EG, en_US } from 'ng-zorro-antd/i18n';
import { provideServiceWorker } from '@angular/service-worker';
import { routes } from './app-routing.module';
import { withUnsavedChangesGuard } from './core/routing/with-unsaved-changes-guard';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { CultureInterceptor } from './core/interceptors/culture.interceptor';
import { ErrorInterceptor } from './core/interceptors/error.interceptor';
import { AppStore } from './core/state/app.store';
import { AppConfigService } from './core/services/config.service';
import { AuthService } from './core/services/auth.service';
import { TranslationService } from './core/services/translation.service';
import { CULTURE_STORAGE_KEY, DEFAULT_CULTURE } from './core/constants/culture.constants';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';

registerLocaleData(ar);

export function nzI18nFactory() {
  return localStorage.getItem(CULTURE_STORAGE_KEY) === 'ar' ? ar_EG : en_US;
}

export const appConfig: ApplicationConfig = {
  providers: [
    // No zone.js: every component is OnPush and keeps its template state in
    // signals, so a view refreshes exactly when a signal it reads changes, a
    // template listener fires, or markForCheck runs.
    provideZonelessChangeDetection(),
    // Without zone.js nothing else forwards uncaught errors and unhandled
    // promise rejections to ErrorHandler.
    provideBrowserGlobalErrorListeners(),
    // Route config is decorated here, not in app-routing.module.ts: the guard is
    // attached to every component route, so any component implementing
    // HasUnsavedChanges is protected without touching its routes file.
    provideRouter(withUnsavedChangesGuard(routes)),
    { provide: NZ_I18N, useFactory: nzI18nFactory },
    // ng-zorro 22 no longer ships a default date engine; nz-date-picker needs one.
    provideNzDateFnsAdapter(),
    importProvidersFrom(
      FormsModule,
      ReactiveFormsModule
    ),
    provideHttpClient(
      withXhr(),
      withInterceptors([
        AuthInterceptor,
        CultureInterceptor,
        ErrorInterceptor
      ])
    ),
    provideTranslateService({
      // useHttpBackend makes translation requests skip the interceptor chain -
      // ErrorInterceptor depends on FeedbackService, which depends on
      // TranslateService, which depends on this loader: routing the loader's
      // requests back through those interceptors is a circular DI (NG0200)
      // that crashes app bootstrap.
      loader: provideTranslateHttpLoader({
        prefix: './assets/i18n/',
        suffix: '.json',
        useHttpBackend: true
      }),
      fallbackLang: DEFAULT_CULTURE
    }),
    NzModalService,
    CookieService,
    AppStore,
    AppConfigService,
    provideAppInitializer(() => inject(AppConfigService).load()),
    provideAppInitializer(() => inject(AuthService).initializePermissions()),
    provideAppInitializer(() => inject(TranslationService).init()),
    // Zoneless stability only waits on Angular's pending tasks (router,
    // HttpClient), so the open SignalR socket no longer holds it back; the
    // ':30000' fallback stays as a safety net if the app never settles.
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000'
    })
  ]
};
