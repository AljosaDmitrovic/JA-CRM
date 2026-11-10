import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { CompaniesApi } from '../core/api/companies.api';
import { LookupsApi } from '../core/api/misc.api';
import { formatDateOnly, money } from '../core/format';
import { CompanyOption, Option } from '../core/models';
import { queryState } from '../core/query-state';
import { SettingsStore } from '../core/settings.store';

export interface SelectFilter {
  key: string;
  label: string;
  options: Option[];
}

/**
 * The filters shared by the vacancy and application lists: company, country, enum selects,
 * technology tags, a date range and a salary range (annual gross in the base currency, D-25).
 * Everything reads and writes the URL query, so it stays in sync with the list.
 */
@Component({
  selector: 'app-filter-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="panel" id="filter-panel" role="region" aria-label="Filters">
      <div class="grid">
        <label class="field">
          <span class="label">Company</span>
          <select class="select" (change)="q.set({ companyId: $any($event.target).value })">
            <option value="">Any company</option>
            @for (c of companies(); track c.id) {
              <option [value]="c.id" [selected]="q.get('companyId')() === '' + c.id">{{ c.name }}{{ c.isArchived ? ' (archived)' : '' }}</option>
            }
          </select>
        </label>
        <label class="field">
          <span class="label">Country</span>
          <select class="select" (change)="q.set({ country: $any($event.target).value })">
            <option value="">Any country</option>
            @for (c of countries(); track c) {
              <option [value]="c" [selected]="q.get('country')() === c">{{ c }}</option>
            }
          </select>
        </label>
        @for (f of selects(); track f.key) {
          <label class="field">
            <span class="label">{{ f.label }}</span>
            <select class="select" (change)="setFilter(f.key, $any($event.target).value)">
              <option value="">Any</option>
              @for (o of f.options; track o.value) {
                <option [value]="o.value" [selected]="q.get(f.key)() === o.value">{{ o.label }}</option>
              }
            </select>
          </label>
        }
        <label class="field">
          <span class="label">Technology</span>
          <input
            class="input"
            list="filter-technologies"
            placeholder="Add a technology…"
            autocomplete="off"
            (keydown.enter)="addTech($any($event.target)); $event.preventDefault()"
            (change)="addTech($any($event.target))"
          />
          <datalist id="filter-technologies">
            @for (t of technologies(); track t) {
              <option [value]="t"></option>
            }
          </datalist>
        </label>
        <fieldset class="field range">
          <legend class="label">Date</legend>
          <select class="select" aria-label="Date field" (change)="q.set({ dateField: $any($event.target).value })">
            @for (o of dateFields(); track o.value) {
              <option [value]="o.value" [selected]="(q.get('dateField')() ?? dateFields()[0].value) === o.value">{{ o.label }}</option>
            }
          </select>
          <input class="input" type="date" aria-label="From" [value]="q.get('from')() ?? ''" (change)="q.set({ from: $any($event.target).value })" />
          <input class="input" type="date" aria-label="To" [value]="q.get('to')() ?? ''" (change)="q.set({ to: $any($event.target).value })" />
        </fieldset>
        <fieldset class="field range">
          <legend class="label">Salary, annual gross {{ settings.baseCurrency() }}</legend>
          <input class="input" type="number" min="0" step="1000" placeholder="Min" aria-label="Minimum annual salary" [value]="q.get('salaryMin')() ?? ''" (change)="q.set({ salaryMin: $any($event.target).value })" />
          <input class="input" type="number" min="0" step="1000" placeholder="Max" aria-label="Maximum annual salary" [value]="q.get('salaryMax')() ?? ''" (change)="q.set({ salaryMax: $any($event.target).value })" />
        </fieldset>
      </div>
      <p class="hint subtle">
        The salary filter compares annual gross amounts in {{ settings.baseCurrency() }}. Net amounts, unknown types and currencies without a rate never match it.
      </p>
    </div>
  `,
  styles: `
    .panel {
      margin-bottom: var(--s-4);
      padding: var(--s-4);
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      background: var(--bg-subtle);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: var(--s-3) var(--s-4);
    }
    .range {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(90px, 1fr));
      gap: var(--s-2);
      margin: 0;
      padding: 0;
      border: 0;
      min-width: 0;
      grid-column: span 2;

      legend {
        float: left;
        width: 100%;
        margin-bottom: 6px;
      }

      @media (max-width: 640px) {
        grid-column: span 1;
      }
    }
    .hint {
      margin-top: var(--s-3);
      font-size: var(--text-xs);
    }
  `,
})
export class FilterPanel implements OnInit {
  protected readonly settings = inject(SettingsStore);
  private readonly companiesApi = inject(CompaniesApi);
  private readonly lookupsApi = inject(LookupsApi);
  protected readonly q = queryState();

  readonly selects = input<SelectFilter[]>([]);
  readonly dateFields = input.required<Option[]>();

  protected readonly companies = signal<CompanyOption[]>([]);
  protected readonly countries = signal<string[]>([]);
  protected readonly technologies = signal<string[]>([]);

  async ngOnInit(): Promise<void> {
    const [companies, lookups] = await Promise.all([this.companiesApi.options(true), this.lookupsApi.get()]).catch(() => [[], null] as const);
    this.companies.set([...companies]);
    this.countries.set(lookups?.countries ?? []);
    this.technologies.set(lookups?.technologies ?? []);
  }

  protected setFilter(key: string, value: string): void {
    this.q.set({ [key]: value });
  }

  protected addTech(field: HTMLInputElement): void {
    const value = field.value.trim();
    field.value = '';
    if (!value) return;
    const current = this.q.all('tech')();
    if (!current.some((t) => t.toLowerCase() === value.toLowerCase())) {
      this.q.set({ tech: [...current, value] });
    }
  }
}

export interface ActiveFilter {
  key: string;
  label: string;
  /** For array params (tech), the single value to remove. */
  value?: string;
}

/** Describes the active filters as removable chips. Call in an injection context. */
export function activeFilters(selects: () => SelectFilter[], dateFields: () => Option[]) {
  const q = queryState();
  const settings = inject(SettingsStore);
  const companies = signal<CompanyOption[]>([]);
  void inject(CompaniesApi).options(true).then((c) => companies.set(c), () => undefined);

  const chips = computed<ActiveFilter[]>(() => {
    const p = q.params();
    const list: ActiveFilter[] = [];
    const companyId = p.get('companyId');
    if (companyId) {
      list.push({ key: 'companyId', label: `Company: ${companies().find((c) => String(c.id) === companyId)?.name ?? '…'}` });
    }
    if (p.get('country')) list.push({ key: 'country', label: `Country: ${p.get('country')}` });
    for (const f of selects()) {
      const value = p.get(f.key);
      if (value) list.push({ key: f.key, label: `${f.label}: ${f.options.find((o) => o.value === value)?.label ?? value}` });
    }
    for (const tech of p.getAll('tech')) list.push({ key: 'tech', value: tech, label: tech });
    const from = p.get('from');
    const to = p.get('to');
    if (from || to) {
      const field = dateFields().find((d) => d.value === p.get('dateField'))?.label ?? dateFields()[0].label;
      const format = settings.dateFormat();
      list.push({
        key: 'date',
        label: `${field}: ${from ? formatDateOnly(from, format) : '…'} – ${to ? formatDateOnly(to, format) : '…'}`,
      });
    }
    const min = p.get('salaryMin');
    const max = p.get('salaryMax');
    if (min || max) {
      const base = settings.baseCurrency();
      list.push({
        key: 'salary',
        label: `Salary: ${min ? money(+min, base) : '…'} – ${max ? money(+max, base) : '…'}`,
      });
    }
    return list;
  });

  return {
    chips,
    remove(chip: ActiveFilter): void {
      if (chip.key === 'tech') {
        q.set({ tech: q.params().getAll('tech').filter((t) => t !== chip.value) });
      } else if (chip.key === 'date') {
        q.set({ from: null, to: null, dateField: null });
      } else if (chip.key === 'salary') {
        q.set({ salaryMin: null, salaryMax: null });
      } else {
        q.set({ [chip.key]: null });
      }
    },
  };
}
