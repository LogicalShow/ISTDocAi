export const SUPPORTED_CULTURES = ['en', 'ar'] as const;
export type Culture = typeof SUPPORTED_CULTURES[number];
export const DEFAULT_CULTURE: Culture = 'en';
export const CULTURE_STORAGE_KEY = 'culture';

export function isSupportedCulture(value: string | null): value is Culture {
  return SUPPORTED_CULTURES.includes(value as Culture);
}
