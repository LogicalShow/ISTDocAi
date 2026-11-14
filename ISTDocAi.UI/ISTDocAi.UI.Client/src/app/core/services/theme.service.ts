import { Injectable, signal } from '@angular/core';

export type AppTheme = 'light' | 'dark';

const THEME_STORAGE_KEY = 'app_theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<AppTheme>('light');
  private initialized = false;

  init(): void {
    if (this.initialized) return;

    const storedTheme = localStorage.getItem(THEME_STORAGE_KEY);
    const preferredTheme: AppTheme = window.matchMedia('(prefers-color-scheme: dark)').matches
      ? 'dark'
      : 'light';

    this.applyTheme(storedTheme === 'light' || storedTheme === 'dark' ? storedTheme : preferredTheme);
    this.initialized = true;
  }

  toggle(): void {
    this.setTheme(this.theme() === 'dark' ? 'light' : 'dark');
  }

  setTheme(theme: AppTheme): void {
    this.applyTheme(theme);
    localStorage.setItem(THEME_STORAGE_KEY, theme);
  }

  private applyTheme(theme: AppTheme): void {
    document.documentElement.dataset['theme'] = theme;
    document.documentElement.style.colorScheme = theme;
    this.theme.set(theme);
    this.syncBrowserThemeColor();
  }

  private syncBrowserThemeColor(): void {
    const meta = document.querySelector<HTMLMetaElement>('meta[name="theme-color"]');
    if (!meta) return;

    const primary = getComputedStyle(document.documentElement)
      .getPropertyValue('--general-primary')
      .trim();

    if (primary) meta.content = primary;
  }
}
