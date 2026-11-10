import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { VacanciesApi } from '../../core/api/vacancies.api';
import { Loadable } from '../../core/loadable';
import { MetaService } from '../../core/meta.service';
import { ArchivedFilter, Option, Paged, VacancyListItem } from '../../core/models';
import { queryState } from '../../core/query-state';
import { ShortcutsService } from '../../core/shortcuts.service';
import { EmptyState } from '../../shared/empty-state';
import { FilterPanel, SelectFilter, activeFilters } from '../../shared/filter-panel';
import { Icon } from '../../shared/icon';
import { Pager, SortHeader } from '../../shared/pager';
import { AppDatePipe } from '../../shared/pipes';
import { SalaryView } from '../../shared/salary-view';
import { StatusBadge } from '../../shared/status-badge';

const PAGE_SIZE = 50;
const FILTER_KEYS = ['search', 'archived', 'companyId', 'country', 'workArrangement', 'openToMyLocation', 'seniority', 'employmentType', 'tech', 'from', 'to', 'salaryMin', 'salaryMax', 'hasApplication'];

@Component({
  selector: 'app-vacancies-page',
  imports: [RouterLink, Icon, EmptyState, Pager, SortHeader, FilterPanel, SalaryView, StatusBadge, AppDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page wide">
      <header class="page-header">
        <div class="title-block">
          <h1>Vacancies</h1>
          <p class="subtitle">{{ subtitle() }}</p>
        </div>
        <div class="actions">
          <a class="btn btn-primary" routerLink="/vacancies/new"><app-icon name="plus" />Add vacancy<kbd>C</kbd></a>
        </div>
      </header>

      <div class="toolbar">
        <label class="search">
          <app-icon name="search" [size]="14" />
          <span class="visually-hidden">Search vacancies</span>
          <input #search class="input" type="search" placeholder="Search title, company, technology, location…" [value]="q.get('search')() ?? ''" (input)="onSearch($any($event.target).value)" />
        </label>
        <button type="button" class="btn" [attr.aria-expanded]="showFilters()" aria-controls="filter-panel" (click)="showFilters.set(!showFilters())">
          <app-icon name="filter" />Filters
          @if (filters.chips().length) {
            <span class="count">{{ filters.chips().length }}</span>
          }
        </button>
        <div class="segmented" role="group" aria-label="Application">
          <button type="button" [attr.aria-pressed]="!q.get('hasApplication')()" (click)="q.set({ hasApplication: null })">All</button>
          <button type="button" [attr.aria-pressed]="q.get('hasApplication')() === 'false'" (click)="q.set({ hasApplication: 'false' })">Not applied</button>
          <button type="button" [attr.aria-pressed]="q.get('hasApplication')() === 'true'" (click)="q.set({ hasApplication: 'true' })">With application</button>
        </div>
        <span class="spacer"></span>
        <select class="select" aria-label="Archived" (change)="q.set({ archived: $any($event.target).value === 'Active' ? null : $any($event.target).value })">
          @for (o of archivedOptions; track o.value) {
            <option [value]="o.value" [selected]="archived() === o.value">{{ o.label }}</option>
          }
        </select>
      </div>

      @if (showFilters()) {
        <app-filter-panel [selects]="selects()" [dateFields]="dateFields" />
      }
      @if (filters.chips().length) {
        <div class="active-filters">
          @for (chip of filters.chips(); track chip.key + (chip.value ?? '')) {
            <button type="button" class="chip removable" (click)="filters.remove(chip)" [attr.aria-label]="'Remove filter ' + chip.label">
              {{ chip.label }}<app-icon name="x" [size]="12" />
            </button>
          }
          <button type="button" class="btn btn-ghost btn-sm" (click)="q.clear()">Clear all</button>
        </div>
      }

      @if (list.error(); as error) {
        <app-empty-state icon="circle-alert" tone="danger" title="Could not load vacancies" [message]="error">
          <button type="button" class="btn" (click)="reload()">Try again</button>
        </app-empty-state>
      } @else if (list.value(); as page) {
        @if (page.items.length) {
          <div class="table-wrap">
            <table class="table" [class.dim]="list.loading()">
              <thead>
                <tr>
                  <th><app-sort-header key="title" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Vacancy</app-sort-header></th>
                  <th class="hide-sm">Location</th>
                  <th class="hide-md">Technologies</th>
                  <th><app-sort-header key="salary" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Salary</app-sort-header></th>
                  <th class="hide-sm"><app-sort-header key="discovered" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Discovered</app-sort-header></th>
                  <th class="hide-sm"><app-sort-header key="deadline" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Deadline</app-sort-header></th>
                  <th>Application</th>
                </tr>
              </thead>
              <tbody>
                @for (v of page.items; track v.id) {
                  <tr class="clickable" [class.archived]="v.isArchived" (click)="open(v, $event)">
                    <td>
                      <a class="primary-cell" [routerLink]="['/vacancies', v.id]">{{ v.title }}</a>
                      @if (v.isArchived) {
                        <span class="tag-flag">Archived</span>
                      }
                      <span class="secondary">{{ v.companyName }}{{ v.seniority ? ' · ' + meta.label('seniorities', v.seniority) : '' }}</span>
                    </td>
                    <td class="hide-sm">
                      {{ join([v.city, v.country]) }}
                      <span class="secondary">
                        {{ meta.label('workArrangements', v.workArrangement) }}
                        @if (v.openToMyLocation === 'No') {
                          <span class="tag-flag warn">Not open to me</span>
                        }
                      </span>
                    </td>
                    <td class="hide-md">
                      <div class="chips">
                        @for (t of v.requiredTechnologies.slice(0, 3); track t) {
                          <span class="chip">{{ t }}</span>
                        }
                        @if (v.requiredTechnologies.length > 3) {
                          <span class="subtle">+{{ v.requiredTechnologies.length - 3 }}</span>
                        }
                      </div>
                    </td>
                    <td><app-salary-view [range]="v.postedSalary" [normalized]="v.normalizedSalary" /></td>
                    <td class="hide-sm nowrap subtle">{{ v.dateDiscovered | appDate }}</td>
                    <td class="hide-sm nowrap" [class.due-soon]="dueSoon(v)">{{ v.applicationDeadline | appDate }}</td>
                    <td>
                      @if (v.applicationStatus) {
                        <app-status-badge [status]="v.applicationStatus" />
                      } @else {
                        <span class="subtle">—</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <app-pager [page]="page.page" [pageSize]="page.pageSize" [total]="page.total" (pageChange)="q.set({ page: $event })" />
        } @else if (filtered()) {
          <app-empty-state icon="search" title="No vacancies match" message="Try a different search or remove some filters.">
            <button type="button" class="btn" (click)="q.clear()">Clear filters</button>
          </app-empty-state>
        } @else {
          <app-empty-state icon="briefcase" title="No vacancies yet" message="Save interesting postings with their full description, then convert them into applications when you apply.">
            <a class="btn btn-primary" routerLink="/vacancies/new"><app-icon name="plus" />Add vacancy</a>
          </app-empty-state>
        }
      } @else {
        <div class="skeleton-table" aria-busy="true" aria-label="Loading vacancies">
          @for (i of [1, 2, 3, 4, 5, 6]; track i) {
            <span class="skeleton"></span>
          }
        </div>
      }
    </div>
  `,
})
export class VacanciesPage {
  protected readonly meta = inject(MetaService);
  private readonly api = inject(VacanciesApi);
  private readonly router = inject(Router);
  protected readonly q = queryState();
  protected readonly list = new Loadable<Paged<VacancyListItem>>();
  protected readonly showFilters = signal(false);
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>('search');
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly archivedOptions: { value: ArchivedFilter; label: string }[] = [
    { value: 'Active', label: 'Active' },
    { value: 'Archived', label: 'Archived only' },
    { value: 'All', label: 'Active and archived' },
  ];
  protected readonly dateFields: Option[] = [
    { value: 'discovered', label: 'Discovered' },
    { value: 'deadline', label: 'Deadline' },
  ];
  protected readonly selects = computed<SelectFilter[]>(() => [
    { key: 'workArrangement', label: 'Work arrangement', options: this.meta.options('workArrangements') },
    { key: 'openToMyLocation', label: 'Open to my location', options: this.meta.options('triStates') },
    { key: 'seniority', label: 'Seniority', options: this.meta.options('seniorities') },
    { key: 'employmentType', label: 'Employment type', options: this.meta.options('employmentTypes') },
  ]);
  protected readonly filters = activeFilters(() => this.selects(), () => this.dateFields);

  protected readonly archived = computed(() => (this.q.get('archived')() as ArchivedFilter | null) ?? 'Active');
  protected readonly sort = computed(() => this.q.get('sort')() ?? 'created');
  protected readonly dir = computed(() => (this.q.get('dir')() === 'asc' ? 'asc' : 'desc'));
  protected readonly filtered = computed(() => FILTER_KEYS.some((k) => this.q.params().has(k)));
  protected readonly subtitle = computed(() => {
    const total = this.list.value()?.total;
    return total === undefined ? ' ' : `${total} ${total === 1 ? 'vacancy' : 'vacancies'}${this.filtered() ? ' match' : ''}`;
  });

  private readonly query = computed(() => {
    const p = this.q.params();
    const one = (k: string) => p.get(k);
    return {
      search: one('search'), archived: one('archived'), companyId: one('companyId'), country: one('country'),
      workArrangement: one('workArrangement'), openToMyLocation: one('openToMyLocation'), seniority: one('seniority'),
      employmentType: one('employmentType'), tech: p.getAll('tech'), dateField: one('dateField'), from: one('from'), to: one('to'),
      salaryMin: one('salaryMin'), salaryMax: one('salaryMax'), hasApplication: one('hasApplication'),
      sort: one('sort'), dir: one('dir'), page: one('page'), pageSize: PAGE_SIZE,
    };
  });

  constructor() {
    effect(() => {
      const query = this.query();
      void this.list.load(() => this.api.list(query));
    });
    inject(ShortcutsService).register([
      { keys: 'c', description: 'Add vacancy', group: 'Vacancies', run: () => void this.router.navigateByUrl('/vacancies/new') },
      { keys: '/', description: 'Search vacancies', group: 'Vacancies', run: () => this.searchField()?.nativeElement.focus() },
      { keys: 'f', description: 'Show or hide filters', group: 'Vacancies', run: () => this.showFilters.update((v) => !v) },
    ]);
  }

  protected reload(): void {
    void this.list.load(() => this.api.list(this.query()));
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.q.set({ search: value.trim() }), 200);
  }

  protected open(v: VacancyListItem, event: MouseEvent): void {
    if ((event.target as HTMLElement).closest('a, button')) return;
    void this.router.navigate(['/vacancies', v.id]);
  }

  protected join(parts: (string | null | undefined)[]): string {
    return parts.filter(Boolean).join(', ');
  }

  protected dueSoon(v: VacancyListItem): boolean {
    if (!v.applicationDeadline || v.applicationId) return false;
    const days = (new Date(v.applicationDeadline).getTime() - Date.now()) / 86_400_000;
    return days >= -1 && days <= 7;
  }
}
