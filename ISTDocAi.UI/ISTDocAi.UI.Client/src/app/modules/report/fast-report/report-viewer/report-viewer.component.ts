import { Component, computed, inject, input } from '@angular/core';

import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { AppConfigService } from '../../../../core/services/config.service';

@Component({
  selector: 'app-report-viewer',
  imports: [],
  templateUrl: './report-viewer.component.html',
  styles: `
    .report-viewer-frame {
      width: 100%;
      height: 100vh;
      border: 0;
    }
  `
})
export class ReportViewerComponent {
  private sanitizer = inject(DomSanitizer);
  private configService = inject(AppConfigService);

  readonly invoiceId = input<string>();

  readonly url = computed<SafeResourceUrl | null>(() => {
    const invoiceId = this.invoiceId();
    if (!invoiceId) {
      return null;
    }
    const baseUrl = this.configService.apiBaseUrl;
    return this.sanitizer.bypassSecurityTrustResourceUrl(
      `${baseUrl}/fastreport/invoice?id=${invoiceId}`
    );
  });
}
