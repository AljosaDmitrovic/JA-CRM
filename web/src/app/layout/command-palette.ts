import { DialogRef } from '@angular/cdk/dialog';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injectable,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { Dialogs } from '../core/dialogs';
import { MetaService } from '../core/meta.service';
import { SearchResults } from '../core/models';
import { displayKeys } from '../core/shortcuts.service';
import { Icon, IconName } from '../shared/icon';
import { QuickActions } from './quick-actions';
import { NAV_ITEMS } from './shell';

interface PaletteItem {
  id: string;
  group: string;
  label: string;
  detail?: string;
  icon: IconName;
  keys?: string;
  run: () => void;
}

const SEARCH_DEBOUNCE_MS = 120;

@Component({
  selector: 'app-command-palette',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './command-palette.html',
  styleUrl: './command-palette.scss',
})
export class CommandPalette {
  private readonly ref = inject(DialogRef);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly quick = inject(QuickActions);
  private readonly meta = inject(MetaService);

  protected readonly query = signal('');
  protected readonly results = signal<SearchResults | null>(null);
  protected readonly searching = signal(false);
  protected readonly activeIndex = signal(0);
  private readonly list = viewChild<ElementRef<HTMLElement>>('list');
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  private searchSeq = 0;

  private readonly commands: PaletteItem[] = [
    ...this.quick.paletteActions().map((a) => ({ ...a, group: 'Actions' })),
    ...NAV_ITEMS.map((item) => ({
      id: `nav:${item.path}`,
      group: 'Navigation',
      label: `Go to ${item.label}`,
      icon: item.icon,
      keys: item.keys,
      run: () => void this.router.navigateByUrl(item.path),
    })),
  ];

  protected readonly items = computed<PaletteItem[]>(() => {
    const q = this.query().trim().toLowerCase();
    const commands = q ? this.commands.filter((c) => c.label.toLowerCase().includes(q)) : this.commands;
    const r = this.results();
    const found: PaletteItem[] = !q || !r
      ? []
      : [
          ...r.applications.map((a) => ({
            id: `app:${a.id}`,
            group: 'Applications',
            label: a.title,
            detail: `${a.companyName} · ${this.meta.statusLabel(a.status)}${a.isArchived ? ' · Archived' : ''}`,
            icon: 'kanban' as IconName,
            run: () => void this.router.navigate(['/applications', a.id]),
          })),
          ...r.vacancies.map((v) => ({
            id: `vac:${v.id}`,
            group: 'Vacancies',
            label: v.title,
            detail: `${v.companyName}${v.isArchived ? ' · Archived' : ''}`,
            icon: 'briefcase' as IconName,
            run: () => void this.router.navigate(['/vacancies', v.id]),
          })),
          ...r.companies.map((c) => ({
            id: `com:${c.id}`,
            group: 'Companies',
            label: c.name,
            detail: [c.country, c.isArchived ? 'Archived' : null].filter(Boolean).join(' · ') || undefined,
            icon: 'building' as IconName,
            run: () => void this.router.navigate(['/companies', c.id]),
          })),
        ];
    // Records first when searching; commands first when browsing.
    return q ? [...found, ...commands] : commands;
  });

  protected readonly groups = computed(() => {
    const groups: { name: string; items: { item: PaletteItem; index: number }[] }[] = [];
    this.items().forEach((item, index) => {
      let group = groups.find((g) => g.name === item.group);
      if (!group) {
        groups.push((group = { name: item.group, items: [] }));
      }
      group.items.push({ item, index });
    });
    return groups;
  });

  protected readonly activeId = computed(() => this.items()[this.activeIndex()]?.id ?? null);

  constructor() {
    effect(() => {
      // Keep the active option visible while moving with the arrow keys.
      const id = this.activeId();
      const list = this.list()?.nativeElement;
      if (id && list) {
        list.querySelector(`[data-id="${CSS.escape(id)}"]`)?.scrollIntoView({ block: 'nearest' });
      }
    });
  }

  protected onInput(value: string): void {
    this.query.set(value);
    this.activeIndex.set(0);
    clearTimeout(this.searchTimer);
    const q = value.trim();
    if (!q) {
      this.results.set(null);
      this.searching.set(false);
      return;
    }
    this.searching.set(true);
    const seq = ++this.searchSeq;
    this.searchTimer = setTimeout(() => {
      this.http.get<SearchResults>('/api/search', { params: { q } }).subscribe({
        next: (results) => {
          if (seq === this.searchSeq) {
            this.results.set(results);
            this.searching.set(false);
            this.activeIndex.set(0);
          }
        },
        error: () => seq === this.searchSeq && this.searching.set(false),
      });
    }, SEARCH_DEBOUNCE_MS);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.items().length;
    if (event.key === 'ArrowDown' || (event.key === 'n' && event.ctrlKey)) {
      event.preventDefault();
      this.activeIndex.update((i) => (count ? (i + 1) % count : 0));
    } else if (event.key === 'ArrowUp' || (event.key === 'p' && event.ctrlKey)) {
      event.preventDefault();
      this.activeIndex.update((i) => (count ? (i - 1 + count) % count : 0));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const item = this.items()[this.activeIndex()];
      if (item) {
        this.run(item);
      }
    }
  }

  protected run(item: PaletteItem): void {
    this.ref.close();
    item.run();
  }

  protected keys(keys: string): string[] {
    return displayKeys(keys);
  }
}

/** Opens and closes the palette (one instance at a time). */
@Injectable({ providedIn: 'root' })
export class CommandPaletteLauncher {
  private readonly dialogs = inject(Dialogs);
  private ref: DialogRef<unknown, CommandPalette> | null = null;

  open(): void {
    if (this.ref) {
      return;
    }
    this.ref = this.dialogs.open(CommandPalette, { width: '620px', position: 'top' });
    this.ref.closed.subscribe(() => (this.ref = null));
  }

  toggle(): void {
    if (this.ref) {
      this.ref.close();
    } else {
      this.open();
    }
  }
}
