import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Dashboard, ImportSummary, Lookups } from '../models';

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  get(): Promise<Dashboard> {
    return firstValueFrom(this.http.get<Dashboard>('/api/dashboard'));
  }
}

/** Distinct values in use (countries, cities, industries, technologies) for form suggestions. */
@Injectable({ providedIn: 'root' })
export class LookupsApi {
  private readonly http = inject(HttpClient);

  get(): Promise<Lookups> {
    return firstValueFrom(this.http.get<Lookups>('/api/lookups'));
  }
}

export type CsvEntity = 'companies' | 'vacancies' | 'applications' | 'status-history';

@Injectable({ providedIn: 'root' })
export class DataApi {
  private readonly http = inject(HttpClient);

  /** Downloads go through the browser (same-origin GET with the session cookie). */
  exportUrl(): string {
    return '/api/data/export';
  }

  csvUrl(entity: CsvEntity): string {
    return `/api/data/export/${entity}.csv`;
  }

  import(file: File): Promise<ImportSummary> {
    return firstValueFrom(
      this.http.post<ImportSummary>('/api/data/import', file, {
        params: { confirm: 'replace-all' },
        headers: { 'Content-Type': 'application/json' },
      }),
    );
  }
}
