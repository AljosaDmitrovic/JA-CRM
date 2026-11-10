import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CompaniesApi } from '../../core/api/companies.api';
import { LookupsApi } from '../../core/api/misc.api';
import { Loadable } from '../../core/loadable';
import { MetaService } from '../../core/meta.service';
import { ArchivedFilter, CompanyListItem, Paged } from '../../core/models';
import { queryState } from '../../core/query-state';
import { ShortcutsService } from '../../core/shortcuts.service';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { Pager, SortHeader } from '../../shared/pager';
import { AppTimestampPipe } from '../../shared/pipes';
import { Rating } from '../../shared/rating';
import { QuickActions } from '../../layout/quick-actions';

const PAGE_SIZE = 50;

@Component({
  selector: 'app-companies-page',
  imports: [RouterLink, Icon, EmptyState, Pager, SortHeader, Rating, AppTimestampPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div class="title-block">
          <h1>Companies</h1>
          <p class="subtitle">{{ subtitle() }}</p>
        </div>
        <div class="actions">
          <button type="button" class="btn btn-primary" (click)="add()"><app-icon name="plus" />Add company<kbd>C</kbd></button>
        </div>
      </header>

      <div class="toolbar">
        <label class="search">
          <app-icon name="search" [size]="14" />
          <span class="visually-hidden">Search companies</span>
          <input #search class="input" type="search" placeholder="Search name, industry, location, notes…" [value]="q.get('search')() ?? ''" (input)="onSearch($any($event.target).value)" />
        </label>
        <select class="select" aria-label="Country" (change)="q.set({ country: $any($event.target).value })">
          <option value="">All countries</option>
          @for (c of countries(); track c) {
            <option [value]="c" [selected]="q.get('country')() === c">{{ c }}</option>
          }
        </select>
        <div class="segmented" role="group" aria-label="Archived">
          @for (option of archivedOptions; track option.value) {
            <button type="button" [attr.aria-pressed]="archived() === option.value" (click)="q.set({ archived: option.value === 'Active' ? null : option.value })">
              {{ option.label }}
            </button>
          }
        </div>
      </div>

      @if (list.error(); as error) {
        <app-empty-state icon="circle-alert" tone="danger" title="Could not load companies" [message]="error">
          <button type="button" class="btn" (click)="reload()">Try again</button>
        </app-empty-state>
      } @else if (list.value(); as page) {
        @if (page.items.length) {
          <div class="table-wrap">
            <table class="table" [class.dim]="list.loading()">
              <thead>
                <tr>
                  <th><app-sort-header key="name" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Name</app-sort-header></th>
                  <th class="hide-sm">Industry</th>
                  <th><app-sort-header key="country" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Location</app-sort-header></th>
                  <th class="hide-sm">Size</th>
                  <th class="hide-sm"><app-sort-header key="rating" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Rating</app-sort-header></th>
                  <th class="num"><app-sort-header key="applications" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Applications</app-sort-header></th>
                  <th class="hide-sm"><app-sort-header key="createdAt" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="q.set($event)">Added</app-sort-header></th>
                </tr>
              </thead>
              <tbody>
                @for (c of page.items; track c.id) {
                  <tr class="clickable" [class.archived]="c.isArchived" (click)="open(c, $event)">
                    <td>
                      <a class="primary-cell" [routerLink]="['/companies', c.id]">{{ c.name }}</a>
                      @if (c.isArchived) {
                        <span class="tag-flag">Archived</span>
                      }
                      @if (c.website) {
                        <span class="secondary truncate">{{ domain(c.website) }}</span>
                      }
                    </td>
                    <td class="hide-sm">{{ c.industry }}</td>
                    <td>{{ location(c) }}</td>
                    <td class="hide-sm nowrap">{{ meta.label('companySizes', c.size) }}</td>
                    <td class="hide-sm"><app-rating [value]="c.rating" /></td>
                    <td class="num">
                      {{ c.applicationCount }}
                      @if (c.activeApplicationCount) {
                        <span class="subtle">· {{ c.activeApplicationCount }} active</span>
                      }
                    </td>
                    <td class="hide-sm nowrap subtle">{{ c.createdAt | appTimestamp }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <app-pager [page]="page.page" [pageSize]="page.pageSize" [total]="page.total" (pageChange)="q.set({ page: $event })" />
        } @else if (filtered()) {
          <app-empty-state icon="search" title="No companies match" message="Try a different search or clear the filters.">
            <button type="button" class="btn" (click)="q.clear()">Clear filters</button>
          </app-empty-state>
        } @else {
          <app-empty-state icon="building" title="No companies yet" message="Companies hold your vacancies and applications. Only a name is needed to start.">
            <button type="button" class="btn btn-primary" (click)="add()"><app-icon name="plus" />Add company</button>
          </app-empty-state>
        }
      } @else {
        <div class="skeleton-table" aria-busy="true" aria-label="Loading companies">
          @for (i of [1, 2, 3, 4, 5, 6]; track i) {
            <span class="skeleton"></span>
          }
        </div>
      }
    </div>
  `,
})
export class CompaniesPage {
  protected readonly meta = inject(MetaService);
  private readonly api = inject(CompaniesApi);
  private readonly lookups = inject(LookupsApi);
  private readonly quick = inject(QuickActions);
  private readonly router = inject(Router);
  protected readonly q = queryState();
  protected readonly list = new Loadable<Paged<CompanyListItem>>();
  protected readonly countries = signal<string[]>([]);
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>('search');
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly archivedOptions: { value: ArchivedFilter; label: string }[] = [
    { value: 'Active', label: 'Active' },
    { value: 'Archived', label: 'Archived' },
    { value: 'All', label: 'All' },
  ];
  protected readonly archived = computed(() => (this.q.get('archived')() as ArchivedFilter | null) ?? 'Active');
  protected readonly sort = computed(() => this.q.get('sort')() ?? 'name');
  protected readonly dir = computed(() => (this.q.get('dir')() === 'desc' ? 'desc' : 'asc'));
  protected readonly filtered = computed(() => ['search', 'country', 'archived'].some((k) => this.q.params().has(k)));
  protected readonly subtitle = computed(() => {
    const total = this.list.value()?.total;
    return total === undefined ? ' ' : `${total} ${total === 1 ? 'company' : 'companies'}${this.filtered() ? ' match' : ''}`;
  });

  private readonly query = computed(() => {
    const p = this.q.params();
    return {
      search: p.get('search'),
      country: p.get('country'),
      archived: p.get('archived'),
      sort: p.get('sort'),
      dir: p.get('dir'),
      page: p.get('page'),
      pageSize: PAGE_SIZE,
    };
  });

  constructor() {
    effect(() => {
      const query = this.query();
      void this.list.load(() => this.api.list(query));
    });
    void this.lookups.get().then((l) => this.countries.set(l.countries), () => undefined);
    inject(ShortcutsService).register([
      { keys: 'c', description: 'Add company', group: 'Companies', run: () => this.add() },
      { keys: '/', description: 'Search companies', group: 'Companies', run: () => this.searchField()?.nativeElement.focus() },
    ]);
  }

  protected reload(): void {
    void this.list.load(() => this.api.list(this.query()));
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.q.set({ search: value.trim() }), 200);
  }

  protected add(): void {
    void this.quick.addCompany();
  }

  protected open(company: CompanyListItem, event: MouseEvent): void {
    if ((event.target as HTMLElement).closest('a, button')) return;
    void this.router.navigate(['/companies', company.id]);
  }

  protected location(c: CompanyListItem): string {
    return [c.city, c.country].filter(Boolean).join(', ');
  }

  protected domain(url: string): string {
    return url.replace(/^https?:\/\/(www\.)?/, '').replace(/\/$/, '');
  }
}
