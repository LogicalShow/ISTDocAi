import { DatePipe } from '@angular/common';
import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService } from '../../core/services/translation.service';

@Pipe({
  name: 'dateWithDefault',
  pure: false
})
export class DateWithDefaultPipe implements PipeTransform {
  private readonly translationService = inject(TranslationService);

  transform(
    value: Date | string | number | null | undefined,
    format = 'dd-MM-yyyy HH:mm',
    offset?: string,
    locale?: string,
    defaultValue = '-'
  ): string {
    if (value === null || value === undefined || value === '') {
      return defaultValue;
    }

    const activeLocale = locale || (this.translationService.culture() === 'ar' ? 'ar' : 'en-US');
    const datePipe = new DatePipe(activeLocale);
    const formattedDate = datePipe.transform(value, format, offset, activeLocale);

    return formattedDate ?? defaultValue;
  }
}
