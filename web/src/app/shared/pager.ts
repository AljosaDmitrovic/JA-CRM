import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Icon } from './icon';

@Component({
  selector: 'app-pager',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (total() > 0) {
      <div class="pager">
        <span>{{ from() }}–{{ to() }} of {{ total() }}</span>
        @if (pages() > 1) {
          <div class="buttons">
            <button type="button" class="btn btn-sm btn-icon" [disabled]="page() <= 1" (click)="pageChange.emit(page() - 1)" aria-label="Previous page">
              <app-icon name="chevron-left" />
            </button>
            <button type="button" class="btn btn-sm btn-icon" [disabled]="page() >= pages()" (click)="pageChange.emit(page() + 1)" aria-label="Next page">
              <app-icon name="chevron-right" />
            </button>
          </div>
        }
      </div>
    }
  `,
})
export class Pager {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly pageChange = output<number>();

  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));
  protected readonly from = computed(() => (this.page() - 1) * this.pageSize() + 1);
  protected readonly to = computed(() => Math.min(this.total(), this.page() * this.pageSize()));
}

/** Sortable column header button: shows the direction and announces it. */
@Component({
  selector: 'app-sort-header',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="sort" [class.active]="active()" (click)="toggle()">
      <ng-content />
      @if (active()) {
        <app-icon [name]="dir() === 'asc' ? 'arrow-up' : 'arrow-down'" [size]="12" />
        <span class="visually-hidden">{{ dir() === 'asc' ? ', sorted ascending' : ', sorted descending' }}</span>
      }
    </button>
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
})
export class SortHeader {
  readonly key = input.required<string>();
  readonly sort = input.required<string>();
  readonly dir = input.required<'asc' | 'desc'>();
  /** Direction used when the column is first chosen (text asc, dates/amounts desc). */
  readonly firstDir = input<'asc' | 'desc'>('asc');
  readonly sortChange = output<{ sort: string; dir: 'asc' | 'desc' }>();

  protected readonly active = computed(() => this.sort() === this.key());

  protected toggle(): void {
    const dir = this.active() ? (this.dir() === 'asc' ? 'desc' : 'asc') : this.firstDir();
    this.sortChange.emit({ sort: this.key(), dir });
  }
}
