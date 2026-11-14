export type TranslationParams = Record<string, unknown>;

export interface ToastOptions {
  durationMs?: number;
}

export interface FeedbackOptions extends ToastOptions {
  messageParams?: TranslationParams;
}

export interface ConfirmOptions {
  /** Interpolation params for the message key. */
  params?: TranslationParams;
  /** i18n key for the confirm button label. Defaults to `common.confirm`. */
  okLabelKey?: string;
  /** i18n key for the cancel button label. Defaults to `common.cancel`. */
  cancelLabelKey?: string;
  /** Renders the confirm button red for irreversible actions. Defaults to false. */
  destructive?: boolean;
}
