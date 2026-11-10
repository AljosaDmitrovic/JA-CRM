import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { ApplicationsApi } from '../../core/api/applications.api';
import { Loadable } from '../../core/loadable';
import { MetaService } from '../../core/meta.service';
import { ApplicationListItem, ArchivedFilter, Option, Paged, QueryParams } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { queryState } from '../../core/query-state';
import { ShortcutsService } from '../../core/shortcuts.service';
import { ToastService } from '../../core/toast.service';
import { EmptyState } from '../../shared/empty-state';
import { FilterPanel, SelectFilter, activeFilters } from '../../shared/filter-panel';
import { Icon } from '../../shared/icon';
import { ApplicationBoard, MoveRequest } from './application-board';
import { ApplicationsTable } from './applications-table';
import { QuickActions } from '../../layout/quick-actions';

type View = 'board' | 'table';
const VIEW_KEY = 'jacrm.applications.view';
const PAGE_SIZE = 50;
const FILTER_KEYS = ['search', 'archived', 'status', 'phase', 'companyId', 'country', 'workArrangement', 'openToMyLocation', 'seniority', 'employmentType', 'tech', 'from', 'to', 'salaryMin', 'salaryMax', 'stale'];

@Component({
  selector: 'app-applications-page',
  imports: [Icon, EmptyState, FilterPanel, ApplicationBoard, ApplicationsTable],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './applications-page.html',
  styles: `
    .view-toggle kbd {
      margin-left: 4px;
    }
  `,
})
export class ApplicationsPage {
  protected readonly meta = inject(MetaService);
  private readonly api = inject(ApplicationsApi);
  private readonly quick = inject(QuickActions);
  private readonly toasts = inject(ToastService);
  protected readonly q = queryState();

  protected readonly board = new Loadable<ApplicationListItem[]>();
  protected readonly table = new Loadable<Paged<ApplicationListItem>>();
  protected readonly pending = signal<ReadonlySet<number>>(new Set());
  protected readonly showFilters = signal(false);
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>('search');
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly view = computed<View>(() => {
    const v = this.q.get('view')();
    return v === 'table' || v === 'board' ? v : readView();
  });
  protected readonly archived = computed(() => (this.q.get('archived')() as ArchivedFilter | null) ?? 'Active');
  protected readonly sort = computed(() => this.q.get('sort')() ?? 'updated');
  protected readonly dir = computed(() => (this.q.get('dir')() === 'asc' ? 'asc' : 'desc'));
  protected readonly filtered = computed(() => FILTER_KEYS.some((k) => this.q.params().has(k)));

  protected readonly archivedOptions: { value: ArchivedFilter; label: string }[] = [
    { value: 'Active', label: 'Active' },
    { value: 'Archived', label: 'Archived only' },
    { value: 'All', label: 'Active and archived' },
  ];
  protected readonly dateFields: Option[] = [
    { value: 'created', label: 'Created' },
    { value: 'submitted', label: 'Submitted' },
    { value: 'updated', label: 'Updated' },
  ];
  protected readonly selects = computed<SelectFilter[]>(() => [
    { key: 'phase', label: 'Phase', options: this.meta.meta().phases.map((p) => ({ value: p.value, label: p.label })) },
    { key: 'status', label: 'Status', options: this.meta.meta().statuses.map((s) => ({ value: s.value, label: s.label })) },
    { key: 'workArrangement', label: 'Work arrangement', options: this.meta.options('workArrangements') },
    { key: 'openToMyLocation', label: 'Open to my location', options: this.meta.options('triStates') },
    { key: 'seniority', label: 'Seniority', options: this.meta.options('seniorities') },
    { key: 'employmentType', label: 'Employment type', options: this.meta.options('employmentTypes') },
  ]);
  protected readonly filters = activeFilters(() => this.selects(), () => this.dateFields);

  protected readonly count = computed(() =>
    this.view() === 'board' ? this.board.value()?.length : this.table.value()?.total,
  );
  protected readonly subtitle = computed(() => {
    const n = this.count();
    if (n === undefined) return ' ';
    const archivedNote = this.view() === 'board' ? '' : this.archived() === 'Archived' ? ' archived' : '';
    return `${n}${archivedNote} ${n === 1 ? 'application' : 'applications'}${this.filtered() ? ' match' : ''}`;
  });

  private readonly query = computed<QueryParams>(() => {
    const p = this.q.params();
    const one = (k: string) => p.get(k);
    return {
      search: one('search'), status: p.getAll('status'), phase: p.getAll('phase'), companyId: one('companyId'),
      country: one('country'), workArrangement: one('workArrangement'), openToMyLocation: one('openToMyLocation'),
      seniority: one('seniority'), employmentType: one('employmentType'), tech: p.getAll('tech'), dateField: one('dateField'),
      from: one('from'), to: one('to'), salaryMin: one('salaryMin'), salaryMax: one('salaryMax'), stale: one('stale'),
    };
  });

  constructor() {
    effect(() => {
      const view = this.view();
      const query = this.query();
      const p = this.q.params();
      untracked(() => this.load(view, query, p.get('archived'), p.get('sort'), p.get('dir'), p.get('page')));
    });

    inject(ShortcutsService).register([
      { keys: 'c', description: 'Create application', group: 'Applications', run: () => void this.create() },
      { keys: 'v', description: 'Switch between board and table', group: 'Applications', run: () => this.setView(this.view() === 'board' ? 'table' : 'board') },
      { keys: '/', description: 'Search applications', group: 'Applications', run: () => this.searchField()?.nativeElement.focus() },
      { keys: 'f', description: 'Show or hide filters', group: 'Applications', run: () => this.showFilters.update((v) => !v) },
    ]);
  }

  protected setView(view: View): void {
    try {
      localStorage.setItem(VIEW_KEY, view);
    } catch {
      // Remembering the view is a convenience only.
    }
    // The board always shows active applications, so the archived filter and paging are table-only.
    this.q.set(view === 'board' ? { view, archived: null } : { view });
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.q.set({ search: value.trim() }), 200);
  }

  protected reload(): void {
    const p = this.q.params();
    this.load(this.view(), this.query(), p.get('archived'), p.get('sort'), p.get('dir'), p.get('page'));
  }

  protected create(): void {
    void this.quick.createApplication();
  }

  /** Optimistic move: the card jumps to its new column at once and returns if the save fails. */
  protected async move({ item, status }: MoveRequest): Promise<void> {
    if (item.status === status || this.pending().has(item.id)) return;
    const replace = (next: ApplicationListItem) => {
      this.board.value.update((list) => list?.map((a) => (a.id === next.id ? next : a)));
      this.table.value.update((page) => page && { ...page, items: page.items.map((a) => (a.id === next.id ? next : a)) });
    };
    replace({ ...item, status, phase: this.meta.phaseOf(status), isStale: false });
    this.pending.update((s) => new Set(s).add(item.id));
    try {
      const updated = await this.api.changeStatus(item.id, status);
      replace(updated);
      this.toasts.success(`Moved to ${this.meta.statusLabel(status)}.`, {
        label: 'Undo',
        run: () => void this.move({ item: updated, status: item.status }),
      });
    } catch (error) {
      replace(item);
      this.toasts.error(problemMessage(error, 'Could not change the status.'));
    } finally {
      this.pending.update((s) => {
        const next = new Set(s);
        next.delete(item.id);
        return next;
      });
    }
  }

  private load(view: View, query: QueryParams, archived: string | null, sort: string | null, dir: string | null, page: string | null): void {
    if (view === 'board') {
      void this.board.load(() => this.api.board(query));
    } else {
      void this.table.load(() => this.api.list({ ...query, archived, sort, dir, page, pageSize: PAGE_SIZE }));
    }
  }
}

function readView(): View {
  try {
    return localStorage.getItem(VIEW_KEY) === 'table' ? 'table' : 'board';
  } catch {
    return 'board';
  }
}
