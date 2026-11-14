import { computed, signal } from '@angular/core';

/** Must match the server-side OTP lifetime (OtpDefaults.Lifetime). */
export const OTP_LIFETIME_SECONDS = 60;

/**
 * Ticks a one-time code's remaining lifetime down to zero. Shared by the
 * forgot-password page and the change-password modal so both show the same
 * `m:ss` countdown and expire together with the server. Call `stop()` from
 * the owner's ngOnDestroy.
 */
export class OtpCountdown {
  readonly secondsRemaining = signal(OTP_LIFETIME_SECONDS);
  /** True once the countdown hits zero; the code must be resent before verifying. */
  readonly expired = computed(() => this.secondsRemaining() === 0);
  readonly formattedTime = computed(() => {
    const seconds = this.secondsRemaining();
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
  });

  private intervalId?: ReturnType<typeof setInterval>;

  constructor(private readonly onExpired: () => void) {}

  start(): void {
    this.stop();
    this.secondsRemaining.set(OTP_LIFETIME_SECONDS);
    this.intervalId = setInterval(() => {
      this.secondsRemaining.update(value => Math.max(0, value - 1));
      if (this.secondsRemaining() === 0) {
        this.stop();
        this.onExpired();
      }
    }, 1000);
  }

  stop(): void {
    if (this.intervalId) {
      clearInterval(this.intervalId);
      this.intervalId = undefined;
    }
  }
}
