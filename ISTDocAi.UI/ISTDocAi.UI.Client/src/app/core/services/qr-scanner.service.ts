import { Injectable, OnDestroy } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import jsQR from 'jsqr';

export interface QrScanResult {
  data: string;
  timestamp: Date;
}

interface DetectedBarcode {
  format: string;
  rawValue: string;
}

interface BarcodeDetectorInstance {
  detect(source: CanvasImageSource): Promise<DetectedBarcode[]>;
}

type BarcodeDetectorConstructor = new(options: { formats: string[] }) => BarcodeDetectorInstance;

type WindowWithBarcodeDetector = Window & {
  BarcodeDetector?: BarcodeDetectorConstructor;
};

@Injectable({
  providedIn: 'root'
})
export class QrScannerService implements OnDestroy {
  private stream: MediaStream | null = null;
  private videoElement: HTMLVideoElement | null = null;
  private canvasElement: HTMLCanvasElement | null = null;
  private canvasContext: CanvasRenderingContext2D | null = null;
  private animationId: number | null = null;
  private isScanning$ = new BehaviorSubject<boolean>(false);
  private scanResult$ = new Subject<QrScanResult>();

  // BarcodeDetector API - modern browser QR detection
  private barcodeDetector: BarcodeDetectorInstance | null = null;

  constructor() {
    // Check if BarcodeDetector is available (Chrome/Edge 83+)
    const barcodeWindow = window as WindowWithBarcodeDetector;
    if (barcodeWindow.BarcodeDetector) {
      this.barcodeDetector = new barcodeWindow.BarcodeDetector({ formats: ['qr_code'] });
    }
  }

  get isScanning(): Observable<boolean> {
    return this.isScanning$.asObservable();
  }

  get onScan(): Observable<QrScanResult> {
    return this.scanResult$.asObservable();
  }

  /**
   * Initialize camera and start scanning
   * @param videoElement HTML video element to display camera feed
   * @param canvasElement Canvas for processing (required for jsQR fallback)
   */
  async startScan(videoElement: HTMLVideoElement, canvasElement?: HTMLCanvasElement): Promise<void> {
    if (this.isScanning$.value) {
      throw new Error('Scanner is already running');
    }

    try {
      // Request camera access
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: 'environment', // Use back camera on mobile
          width: { ideal: 1280 },
          height: { ideal: 720 }
        }
      });

      this.videoElement = videoElement;
      this.videoElement.srcObject = this.stream;
      await this.videoElement.play();

      if (canvasElement) {
        this.canvasElement = canvasElement;
        this.canvasContext = this.canvasElement.getContext('2d', { willReadFrequently: true });
      }

      this.isScanning$.next(true);
      this.scan();
    } catch (error) {
      this.handleError(error instanceof Error ? error : undefined);
      throw error;
    }
  }

  /**
   * Stop scanning and release camera
   */
  stopScan(): void {
    if (this.animationId) {
      cancelAnimationFrame(this.animationId);
      this.animationId = null;
    }

    if (this.stream) {
      this.stream.getTracks().forEach(track => track.stop());
      this.stream = null;
    }

    if (this.videoElement) {
      this.videoElement.srcObject = null;
      this.videoElement = null;
    }

    this.canvasElement = null;
    this.canvasContext = null;
    this.isScanning$.next(false);
  }

  /**
   * Check if camera permission is granted
   */
  async checkCameraPermission(): Promise<boolean> {
    try {
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        return false;
      }

      // Try to get permission status
      if (navigator.permissions && navigator.permissions.query) {
        const result = await navigator.permissions.query({ name: 'camera' as PermissionName });
        return result.state === 'granted';
      }

      return true; // Assume available if permissions API not supported
    } catch {
      return false;
    }
  }

  /**
   * Main scan loop
   */
  private async scan(): Promise<void> {
    if (!this.isScanning$.value || !this.videoElement) {
      return;
    }

    try {
      if (this.barcodeDetector) {
        // Use modern BarcodeDetector API (Chrome/Edge)
        const barcodes = await this.barcodeDetector.detect(this.videoElement);
        
        if (barcodes && barcodes.length > 0) {
          const qrCode = barcodes.find(barcode => barcode.format === 'qr_code');
          if (qrCode) {
            this.onQrDetected(qrCode.rawValue);
            return; // Stop scanning after successful detection
          }
        }
      } else {
        // Fallback: Use jsQR for all browsers (including mobile)
        if (this.canvasElement && this.canvasContext) {
          this.canvasElement.width = this.videoElement.videoWidth;
          this.canvasElement.height = this.videoElement.videoHeight;
          
          this.canvasContext.drawImage(
            this.videoElement,
            0,
            0,
            this.canvasElement.width,
            this.canvasElement.height
          );

          const imageData = this.canvasContext.getImageData(
            0,
            0,
            this.canvasElement.width,
            this.canvasElement.height
          );

          const code = this.detectQrFromImageData(imageData);
          if (code) {
            this.onQrDetected(code);
            return;
          }
        }
      }
    } catch (error) {
      console.error('QR scan error:', error);
    }

    // Continue scanning
    this.animationId = requestAnimationFrame(() => this.scan());
  }

  /**
   * Detect QR from image data using jsQR library
   */
  private detectQrFromImageData(imageData: ImageData): string | null {
    const code = jsQR(imageData.data, imageData.width, imageData.height, {
      inversionAttempts: 'dontInvert'
    });
    return code ? code.data : null;
  }

  /**
   * Handle successful QR detection
   */
  private onQrDetected(data: string): void {
    // Extract relevant data from QR code
    let extractedData = data;
    try {
      const parsed = JSON.parse(data);
      if (parsed.Id) {
        extractedData = parsed.Id;
      } else if (parsed.tripSerial) {
        extractedData = parsed.tripSerial;
      } else if (parsed.id) {
        extractedData = parsed.id;
      }
    } catch {
      // Not JSON, use raw data
    }

    this.scanResult$.next({
      data: extractedData,
      timestamp: new Date()
    });
    
    // Auto-stop after successful scan
    this.stopScan();
  }

  /**
   * Handle errors
   */
  private handleError(error?: Error): void {
    let message = 'Camera access denied or unavailable';
    
    if (error?.name === 'NotAllowedError') {
      message = 'Camera permission denied. Please allow camera access.';
    } else if (error?.name === 'NotFoundError') {
      message = 'No camera found on this device.';
    } else if (error?.name === 'NotReadableError') {
      message = 'Camera is already in use by another application.';
    }

    console.error('QR Scanner Error:', message, error);
  }

  /**
   * Cleanup on service destroy
   */
  ngOnDestroy(): void {
    this.stopScan();
    this.isScanning$.complete();
    this.scanResult$.complete();
  }
}
