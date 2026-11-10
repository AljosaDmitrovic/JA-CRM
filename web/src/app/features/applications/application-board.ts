import { CdkDrag, CdkDragDrop, CdkDragPlaceholder, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { MetaService } from '../../core/meta.service';
import { ApplicationListItem, ApplicationPhase, ApplicationStatus } from '../../core/models';
import { Icon } from '../../shared/icon';
import { statusColor } from '../../shared/status-badge';
import { ApplicationCard } from './application-card';

export interface MoveRequest {
  item: ApplicationListItem;
  status: ApplicationStatus;
}

const COLLAPSED_KEY = 'jacrm.board.expandedClosed';

/**
 * Kanban board: one column per status, grouped by phase. Dragging a card to another column (or
 * using its "Move to…" menu) asks the page to change the status. Cards are ordered by the server
 * (priority, then last updated), so sorting inside a column is disabled.
 */
@Component({
  selector: 'app-application-board',
  imports: [CdkDropListGroup, CdkDropList, CdkDrag, CdkDragPlaceholder, ApplicationCard, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="board" cdkDropListGroup>
      @for (phase of columns(); track phase.value) {
        <section class="phase" [attr.aria-label]="phase.label + ' phase'">
          <header class="phase-head">
            <span class="phase-name">{{ phase.label }}</span>
            <span class="subtle">{{ phase.count }}</span>
            @if (phase.value === 'Closed') {
              <button type="button" class="btn btn-ghost btn-sm" (click)="toggleClosed()" [attr.aria-expanded]="closedExpanded()">
                <app-icon [name]="closedExpanded() ? 'chevron-left' : 'chevron-right'" [size]="14" />
                {{ closedExpanded() ? 'Collapse' : 'Expand' }}
              </button>
            }
          </header>
          <div class="columns">
            @for (column of phase.columns; track column.status) {
              <div
                class="column"
                [class.collapsed]="column.collapsed"
                cdkDropList
                [cdkDropListData]="column.status"
                [cdkDropListSortingDisabled]="true"
                (cdkDropListDropped)="onDrop($event)"
                role="list"
                [attr.aria-label]="column.label + ', ' + column.items.length + (column.items.length === 1 ? ' application' : ' applications')"
              >
                <div class="column-head" [title]="column.label">
                  <span class="dot" [style.background]="column.color"></span>
                  <span class="label">{{ column.label }}</span>
                  <span class="count">{{ column.items.length }}</span>
                </div>
                @if (!column.collapsed) {
                  <div class="cards">
                    @for (item of column.items; track item.id) {
                      <app-application-card
                        cdkDrag
                        [cdkDragData]="item"
                        [cdkDragStartDelay]="{ touch: 250, mouse: 0 }"
                        [item]="item"
                        [class.pending]="pending().has(item.id)"
                        (move)="move.emit({ item, status: $event })"
                      >
                        <div class="drop-placeholder" *cdkDragPlaceholder></div>
                      </app-application-card>
                    } @empty {
                      <div class="empty-column">Drop here</div>
                    }
                  </div>
                }
              </div>
            }
          </div>
        </section>
      }
    </div>
  `,
  styleUrl: './application-board.scss',
})
export class ApplicationBoard {
  private readonly meta = inject(MetaService);
  readonly items = input.required<ApplicationListItem[]>();
  /** Ids with a status change in flight (shown dimmed). */
  readonly pending = input<ReadonlySet<number>>(new Set());
  readonly move = output<MoveRequest>();

  protected readonly closedExpanded = signal(readExpanded());

  protected readonly columns = computed(() => {
    const byStatus = new Map<ApplicationStatus, ApplicationListItem[]>();
    for (const item of this.items()) {
      byStatus.set(item.status, [...(byStatus.get(item.status) ?? []), item]);
    }
    return this.meta.meta().phases.map((phase) => {
      const collapsed = phase.value === 'Closed' && !this.closedExpanded();
      const columns = phase.statuses.map((status) => ({
        status,
        label: this.meta.statusLabel(status),
        color: statusColor(status, phase.value),
        items: byStatus.get(status) ?? [],
        collapsed,
      }));
      return {
        value: phase.value as ApplicationPhase,
        label: phase.label,
        count: columns.reduce((sum, c) => sum + c.items.length, 0),
        columns,
      };
    });
  });

  protected onDrop(event: CdkDragDrop<ApplicationStatus, ApplicationStatus, ApplicationListItem>): void {
    if (event.previousContainer === event.container) return;
    this.move.emit({ item: event.item.data, status: event.container.data });
  }

  protected toggleClosed(): void {
    this.closedExpanded.update((v) => !v);
    try {
      localStorage.setItem(COLLAPSED_KEY, String(this.closedExpanded()));
    } catch {
      // Remembering the choice is a convenience only.
    }
  }
}

function readExpanded(): boolean {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === 'true';
  } catch {
    return false;
  }
}
