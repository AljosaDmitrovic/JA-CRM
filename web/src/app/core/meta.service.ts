import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApplicationPhase, ApplicationStatus, Meta, Option } from './models';
import { SettingsStore } from './settings.store';

type OptionKey = Exclude<keyof Meta, 'statuses' | 'phases' | 'commonCurrencies'>;

/** Enum values, labels and the status → phase map, loaded once from GET /api/meta so the
 * client never duplicates them. */
@Injectable({ providedIn: 'root' })
export class MetaService {
  private readonly http = inject(HttpClient);
  private readonly data = signal<Meta | null>(null);
  private loading: Promise<void> | null = null;

  readonly meta = computed(() => {
    const meta = this.data();
    if (!meta) {
      throw new Error('Meta has not been loaded yet.');
    }
    return meta;
  });

  private readonly statusMap = computed(() => new Map(this.meta().statuses.map((s) => [s.value, s])));

  load(): Promise<void> {
    this.loading ??= firstValueFrom(this.http.get<Meta>('/api/meta'))
      .then((meta) => this.data.set(meta))
      .catch((error) => {
        this.loading = null;
        throw error;
      });
    return this.loading;
  }

  statusLabel(status: ApplicationStatus | null | undefined): string {
    return status ? (this.statusMap().get(status)?.label ?? status) : '';
  }

  phaseOf(status: ApplicationStatus): ApplicationPhase {
    return this.statusMap().get(status)!.phase;
  }

  options<K extends OptionKey>(key: K): Meta[K] {
    return this.meta()[key];
  }

  label<K extends OptionKey>(key: K, value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    return (this.meta()[key] as Option[]).find((o) => o.value === value)?.label ?? value;
  }
}

/** Loads meta and settings before the authenticated shell renders. */
export const appDataGuard: CanActivateFn = async () => {
  await Promise.all([inject(MetaService).load(), inject(SettingsStore).load()]);
  return true;
};
