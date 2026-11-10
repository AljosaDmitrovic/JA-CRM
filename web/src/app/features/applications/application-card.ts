import { ChangeDetectionStrategy, Component, computed, inject, input, output, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { normalizedText } from '../../core/format';
import { MetaService } from '../../core/meta.service';
import { ApplicationListItem, ApplicationStatus } from '../../core/models';
import { Icon } from '../../shared/icon';
import { TimeAgoPipe } from '../../shared/pipes';
import { PriorityIndicator } from '../../shared/priority';
import { MoveMenu } from './move-menu';

/** One Kanban card. Focusable: Enter opens it, M opens "Move to…". */
@Component({
  selector: 'app-application-card',
  imports: [RouterLink, Icon, PriorityIndicator, MoveMenu, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    tabindex: '0',
    role: 'listitem',
    '[attr.aria-label]': 'ariaLabel()',
    '[class.stale]': 'item().isStale',
    '(keydown)': 'onKeydown($event)',
  },
  template: `
    <div class="top">
      <a class="title" [routerLink]="['/applications', item().id]" tabindex="-1" (click)="$event.stopPropagation()">{{ item().title }}</a>
      <app-move-menu #moveMenu [current]="item().status" [itemLabel]="item().title" (move)="move.emit($event)" />
    </div>
    <div class="company">{{ item().companyName }}</div>
    @if (details()) {
      <div class="meta"><app-icon name="map-pin" [size]="12" />{{ details() }}</div>
    }
    @if (salary()) {
      <div class="meta num"><app-icon name="banknote" [size]="12" />{{ salary() }}</div>
    }
    <div class="footer">
      <app-priority [priority]="item().priority" />
      @if (item().isStale) {
        <span class="tag-flag warn" title="No status change for a while: consider moving it to No Response.">
          <app-icon name="clock" [size]="11" />&nbsp;{{ item().statusChangedAt | timeAgo }}
        </span>
      } @else {
        <span class="subtle updated">{{ item().updatedAt | timeAgo }}</span>
      }
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 3px;
      padding: 10px 10px 8px 12px;
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      background: var(--surface);
      cursor: grab;
      transition: border-color var(--transition), box-shadow var(--transition);
      outline: none;

      &:hover {
        border-color: var(--border-strong);
      }

      &:focus-visible {
        box-shadow: var(--focus-ring);
      }
    }
    .top {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--s-1);
      margin-right: -4px;
    }
    .title {
      padding-top: 2px;
      font-weight: 500;
      line-height: 1.35;
      overflow-wrap: anywhere;

      &:hover {
        text-decoration: underline;
      }
    }
    .company {
      color: var(--text-muted);
      font-size: var(--text-sm);
    }
    .meta {
      display: flex;
      align-items: center;
      gap: 5px;
      color: var(--text-subtle);
      font-size: var(--text-xs);
    }
    .footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: 4px;
      font-size: var(--text-xs);
    }
    .tag-flag {
      display: inline-flex;
      align-items: center;
    }
    app-move-menu {
      opacity: 0;
      transition: opacity var(--transition);
    }
    :host(:hover) app-move-menu,
    :host(:focus-within) app-move-menu,
    :host(:focus) app-move-menu {
      opacity: 1;
    }
    @media (hover: none) {
      app-move-menu {
        opacity: 1;
      }
    }
  `,
})
export class ApplicationCard {
  private readonly meta = inject(MetaService);
  private readonly router = inject(Router);
  readonly item = input.required<ApplicationListItem>();
  readonly move = output<ApplicationStatus>();
  private readonly moveMenu = viewChild.required<MoveMenu>('moveMenu');

  protected readonly details = computed(() => {
    const i = this.item();
    return [i.city ?? i.country, this.meta.label('workArrangements', i.workArrangement)].filter(Boolean).join(' · ');
  });
  protected readonly salary = computed(() => {
    const n = this.item().normalizedSalary;
    if (!n) return '';
    const text = normalizedText(n);
    return text ? `${text}${n.comparability === 'Comparable' ? '' : ' (not comparable)'}` : 'Salary: no exchange rate';
  });
  protected readonly ariaLabel = computed(() => {
    const i = this.item();
    return `${i.title} at ${i.companyName}, ${i.priority} priority${i.isStale ? ', no response for a while' : ''}. Press Enter to open, M to move.`;
  });

  protected onKeydown(event: KeyboardEvent): void {
    if (event.target !== event.currentTarget) return;
    if (event.key === 'Enter') {
      event.preventDefault();
      void this.router.navigate(['/applications', this.item().id]);
    } else if (event.key === 'm' || event.key === 'M') {
      event.preventDefault();
      event.stopPropagation();
      this.moveMenu().open();
    }
  }
}
