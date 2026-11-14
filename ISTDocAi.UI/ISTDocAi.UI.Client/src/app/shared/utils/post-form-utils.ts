import { firstValueFrom } from 'rxjs';
import { FeedbackService } from '../../core/services/feedback.service';
import { ReportTokenService } from '../../core/services/report-token.service';

/**
 * Opens a POST form in a new tab (or specified target).
 * Creates a dynamic form, submits it, then removes it from the DOM.
 *
 * @param url - The form action URL
 * @param fields - The form fields
 * @param target - The target window/tab (default: '_blank')
 */
export function openPostFormInNewTab<T extends object>(
  url: string,
  fields: T,
  target = '_blank'
): void {
  const form = document.createElement('form');
  form.method = 'POST';
  form.action = url;
  form.target = target;

  // Add non-null, non-undefined, non-empty-string fields as hidden inputs
  Object.entries(fields).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = key;
      input.value = String(value);
      form.appendChild(input);
    }
  });

  document.body.appendChild(form);
  form.submit();
  document.body.removeChild(form);
}

/**
 * Opens a server-rendered report (/mvc/Report/...) in a new tab without relying on an auth cookie.
 *
 * The tab is opened synchronously - this must be called directly from the click handler -
 * so popup blockers still see the user gesture. A short-lived report token is then requested
 * and POSTed, together with the report fields, into that tab as the `reportToken` form field.
 *
 * @param url - The report action URL
 * @param fields - The report criteria fields
 */
export async function openReportInNewTab<T extends object>(
  url: string,
  fields: T,
  reportTokenService: ReportTokenService,
  feedbackService: FeedbackService
): Promise<void> {
  const target = `report_${Date.now()}`;
  const reportWindow = window.open('', target);
  if (!reportWindow) {
    feedbackService.warning('feedback.access.popupBlocked.title', 'feedback.access.popupBlocked.message');
    return;
  }

  try {
    const { reportToken } = await firstValueFrom(reportTokenService.getReportToken());
    openPostFormInNewTab(url, { ...fields, reportToken }, target);
  } catch (error) {
    reportWindow.close();
    feedbackService.handleApiError(error);
  }
}
