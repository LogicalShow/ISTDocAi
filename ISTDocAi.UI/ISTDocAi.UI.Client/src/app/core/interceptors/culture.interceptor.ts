import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslationService } from '../services/translation.service';

export const CultureInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url.includes('/assets/i18n/')) {
    return next(req);
  }

  const culture = inject(TranslationService).culture();

  return next(req.clone({
    setHeaders: {
      'X-Culture': culture
    }
  }));
};
