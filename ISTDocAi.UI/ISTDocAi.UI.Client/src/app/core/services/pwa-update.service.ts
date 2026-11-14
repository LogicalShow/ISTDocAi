import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { interval } from 'rxjs';
import { filter } from 'rxjs/operators';
import { FeedbackService } from './feedback.service';

const UPDATE_CHECK_INTERVAL_MS = 30 * 60 * 1000;

@Injectable({
  providedIn: 'root'
})
export class PwaUpdateService {
  private swUpdate = inject(SwUpdate);
  private feedbackService = inject(FeedbackService);
  private destroyRef = inject(DestroyRef);

  private isPrompting = false;

  init(): void {
    // False under ng serve and under any development build, because
    // provideServiceWorker was given enabled: !isDevMode().
    if (!this.swUpdate.isEnabled) {
      return;
    }

    this.swUpdate.versionUpdates
      .pipe(
        filter((event): event is VersionReadyEvent => event.type === 'VERSION_READY'),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.promptReload());

    // The worker can no longer serve a consistent version - typically because
    // the next ng build deleted the previous deployment's hashed chunks.
    // Nothing to prompt about, just get onto the new build.
    this.swUpdate.unrecoverable
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => document.location.reload());

    // Zoneless: a plain interval neither triggers change detection nor keeps
    // ApplicationRef.isStable false, so no zone escape is needed.
    interval(UPDATE_CHECK_INTERVAL_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.swUpdate.checkForUpdate().catch(() => undefined);
      });
  }

  private promptReload(): void {
    if (this.isPrompting) {
      return;
    }
    this.isPrompting = true;

    // Shared confirm dialog (Figma "Delete user dialog2"), same as every other
    // confirmation in the app. Awaitable overload so "Later" / X / Escape can
    // re-arm the prompt for the next VERSION_READY event.
    this.feedbackService
      .confirm(
        'feedback.system.updateAvailable.title',
        'feedback.system.updateAvailable.message',
        { okLabelKey: 'common.reloadNow', cancelLabelKey: 'common.later' }
      )
      .afterClose
      .subscribe(async confirmed => {
        if (confirmed !== true) {
          this.isPrompting = false;
          return;
        }
        await this.swUpdate.activateUpdate();
        document.location.reload();
      });
  }
}
