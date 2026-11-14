import { Component, DestroyRef, ElementRef, OnDestroy, OnInit, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { NzModalModule, NzModalRef } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { QrScannerService } from '../../../core/services/qr-scanner.service';
import { IconComponent } from '../../icons/icon.component';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-qr-scanner-modal',
  imports: [
    NzModalModule,
    NzButtonModule,
    NzSpinModule,
    IconComponent,
    TranslatePipe
],
  providers: [QrScannerService],
  template: `
    <div class="qr-scanner-container">
      @if (error(); as error) {
        <div class="qr-alert qr-alert--error">
          <app-icon name="x-circle-light" iconClass="qr-alert__icon"></app-icon>
          <span>{{ error | translate }}</span>
        </div>
      }
    
      @if (!error() && !isScanning()) {
        <div class="qr-alert qr-alert--info">
          <app-icon name="info-light" iconClass="qr-alert__icon"></app-icon>
          <span>{{ 'shared.qr.startingCamera' | translate }}</span>
        </div>
      }
    
      @if (!error()) {
        <div class="video-container">
          <video
            #videoElement
            autoplay
            playsinline
            [class.scanning]="isScanning()"
          ></video>
          <canvas #canvasElement style="display: none;"></canvas>
          @if (isScanning()) {
            <div class="scan-overlay">
              <div class="scan-frame"></div>
              <p class="scan-instruction">{{ 'shared.qr.instruction' | translate }}</p>
            </div>
          }
        </div>
      }
    
      @if (!error()) {
        <div class="actions">
          <button nz-button nzType="default" (click)="close()">
            {{ 'common.cancel' | translate }}
          </button>
        </div>
      }
    </div>
    `,
  styles: [`
    .qr-scanner-container {
      padding: 20px;
    }

    .video-container {
      position: relative;
      width: 100%;
      max-width: 500px;
      margin: 0 auto;
      background: var(--general-unofficial-camera-surface);
      border-radius: 8px;
      overflow: hidden;
    }

    video {
      width: 100%;
      height: auto;
      display: block;
      max-height: 400px;
      object-fit: cover;
    }

    video.scanning {
      opacity: 1;
    }

    .scan-overlay {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-direction: column;
    }

    .scan-frame {
      width: 250px;
      height: 250px;
      border: 3px solid var(--general-success);
      border-radius: 12px;
      box-shadow: 0 0 0 9999px var(--general-unofficial-camera-scrim);
      animation: pulse 2s infinite;
    }

    @keyframes pulse {
      0%, 100% {
        border-color: var(--general-success);
      }
      50% {
        border-color: var(--color-success-solid-400);
      }
    }

    .scan-instruction {
      margin-top: 20px;
      color: var(--color-primitives-base-white);
      font-size: 14px;
      text-shadow: var(--shadow-text-on-camera);
      background: var(--general-unofficial-backdrop);
      padding: 8px 16px;
      border-radius: 20px;
    }

    .actions {
      margin-top: 16px;
      text-align: center;
    }

    .qr-alert {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 12px;
      border-radius: 8px;
      font-size: 14px;
      margin-bottom: 16px;
    }

    .qr-alert--error {
      background: var(--color-error-solid-100, #ffe2e2);
      color: var(--color-error-solid-600, #dc2626);
    }

    .qr-alert--info {
      background: var(--color-info-solid-100, #e0f5fe);
      color: var(--color-info-solid-700, #0370a1);
    }

    .qr-alert__icon {
      flex-shrink: 0;
    }
  `]
})
export class QrScannerModalComponent implements OnInit, OnDestroy {
  private modal = inject(NzModalRef);
  private qrScannerService = inject(QrScannerService);
  private destroyRef = inject(DestroyRef);

  readonly videoElement = viewChild.required<ElementRef<HTMLVideoElement>>('videoElement');
  readonly canvasElement = viewChild.required<ElementRef<HTMLCanvasElement>>('canvasElement');

  readonly isScanning = signal(false);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    // Wait for view to initialize then start scanning
    // Camera permission will be requested by getUserMedia
    setTimeout(() => this.startScanning(), 100);
  }

  private async startScanning(): Promise<void> {
    try {
      // Subscribe to scan results
      this.qrScannerService.onScan
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((result: { data: string }) => {
          this.modal.close(result.data);
        });

      // Subscribe to scanning status
      this.qrScannerService.isScanning
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((scanning: boolean) => {
          this.isScanning.set(scanning);
        });

      // Start scanning
      await this.qrScannerService.startScan(
        this.videoElement().nativeElement,
        this.canvasElement().nativeElement
      );
    } catch (err) {
      this.error.set(this.getCameraErrorMessage(err instanceof Error ? err : undefined));
      console.error('QR Scanner error:', err);
    }
  }

  private getCameraErrorMessage(error?: Error): string {
    if (error?.name === 'NotAllowedError') {
      return 'shared.qr.errors.permissionDenied';
    }
    if (error?.name === 'NotFoundError') {
      return 'shared.qr.errors.notFound';
    }
    if (error?.name === 'NotReadableError') {
      return 'shared.qr.errors.notReadable';
    }
    return 'shared.qr.errors.unableToStart';
  }

  close(): void {
    this.modal.close(null);
  }

  ngOnDestroy(): void {
    this.qrScannerService.stopScan();
  }
}
