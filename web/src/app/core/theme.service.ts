import { Injectable, signal } from '@angular/core';

export type ThemePreference = 'System' | 'Light' | 'Dark';

const STORAGE_KEY = 'jacrm.theme';

/**
 * Applies the theme by setting data-theme on <html> ('System' removes it, so the CSS media query
 * decides). The choice is cached in localStorage so the next load starts in the right theme;
 * the source of truth is the Settings record on the server.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly preference = signal<ThemePreference>(ThemeService.readCached());

  static applyCached(): void {
    ThemeService.applyPreference(ThemeService.readCached());
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
    ThemeService.applyPreference(preference);
    try {
      localStorage.setItem(STORAGE_KEY, preference);
    } catch {
      // Storage can be unavailable (private mode); the theme still applies for this session.
    }
  }

  private static readCached(): ThemePreference {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      return value === 'Light' || value === 'Dark' ? value : 'System';
    } catch {
      return 'System';
    }
  }

  private static applyPreference(preference: ThemePreference): void {
    const root = document.documentElement;
    if (preference === 'System') {
      delete root.dataset['theme'];
    } else {
      root.dataset['theme'] = preference.toLowerCase();
    }
  }
}
