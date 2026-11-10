import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ExchangeRate, Settings } from './models';
import { ThemeService } from './theme.service';

export type SettingsUpdate = Omit<Settings, 'exchangeRates'>;

/** The single Settings record (theme, base currency, rates, …), shared by every page. */
@Injectable({ providedIn: 'root' })
export class SettingsStore {
  private readonly http = inject(HttpClient);
  private readonly theme = inject(ThemeService);
  private readonly data = signal<Settings | null>(null);

  readonly settings = computed(() => this.data());
  readonly baseCurrency = computed(() => this.data()?.baseCurrency ?? 'EUR');
  readonly dateFormat = computed(() => this.data()?.dateFormat ?? 'DayMonthYear');

  async load(): Promise<void> {
    if (this.data()) {
      return;
    }
    this.apply(await firstValueFrom(this.http.get<Settings>('/api/settings')));
  }

  async update(update: SettingsUpdate): Promise<Settings> {
    const settings = await firstValueFrom(this.http.put<Settings>('/api/settings', update));
    this.apply(settings);
    return settings;
  }

  async saveRates(rates: ExchangeRate[]): Promise<Settings> {
    const settings = await firstValueFrom(this.http.put<Settings>('/api/settings/exchange-rates', { rates }));
    this.apply(settings);
    return settings;
  }

  /** Forces the next load() to fetch again (after an import replaced everything). */
  invalidate(): void {
    this.data.set(null);
  }

  private apply(settings: Settings): void {
    this.data.set(settings);
    this.theme.set(settings.theme);
  }
}
