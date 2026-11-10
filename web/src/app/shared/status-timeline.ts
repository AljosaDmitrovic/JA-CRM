import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MetaService } from '../core/meta.service';
import { StatusHistoryEntry } from '../core/models';
import { AppTimestampPipe, TimeAgoPipe } from './pipes';
import { statusColor } from './status-badge';

/** Vertical timeline of status changes, newest first. */
@Component({
  selector: 'app-status-timeline',
  imports: [RouterLink, AppTimestampPipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="timeline">
      @for (entry of entries(); track entry.id) {
        <li>
          <span class="dot" [style.background]="color(entry)"></span>
          <div class="content">
            <div class="line">
              @if (entry.fromStatus) {
                <span class="muted">{{ meta.statusLabel(entry.fromStatus) }}</span>
                <span class="arrow" aria-label="to">→</span>
                <strong>{{ meta.statusLabel(entry.toStatus) }}</strong>
              } @else {
                <span>Created as <strong>{{ meta.statusLabel(entry.toStatus) }}</strong></span>
              }
              @if (showTitle()) {
                <span class="subtle">·</span>
                <a class="link-quiet truncate" [routerLink]="['/applications', entry.applicationId]">{{ entry.jobTitle }}</a>
              }
            </div>
            <time class="subtle" [attr.datetime]="entry.changedAt" [title]="entry.changedAt | appTimestamp: 'time'">
              {{ entry.changedAt | appTimestamp: 'time' }} · {{ entry.changedAt | timeAgo }}
            </time>
            @if (entry.note) {
              <p class="note">{{ entry.note }}</p>
            }
          </div>
        </li>
      } @empty {
        <li class="empty subtle">No status changes yet.</li>
      }
    </ol>
  `,
  styles: `
    .timeline {
      position: relative;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      position: relative;
      display: flex;
      gap: var(--s-3);
      padding-bottom: var(--s-4);
    }
    li:not(:last-child)::before {
      content: '';
      position: absolute;
      left: 4px;
      top: 14px;
      bottom: 0;
      width: 1px;
      background: var(--border);
    }
    .dot {
      flex: none;
      width: 9px;
      height: 9px;
      margin-top: 6px;
      border-radius: 50%;
      box-shadow: 0 0 0 3px var(--bg);
    }
    .content {
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .line {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 6px;
    }
    .arrow {
      color: var(--text-subtle);
    }
    time {
      font-size: var(--text-sm);
    }
    .note {
      margin-top: var(--s-1);
      padding: var(--s-2) var(--s-3);
      border-radius: var(--radius);
      background: var(--surface-sunken);
      color: var(--text-muted);
      font-size: var(--text-sm);
      white-space: pre-wrap;
    }
    .link-quiet:hover {
      text-decoration: underline;
    }
    .empty {
      padding: 0;
    }
  `,
})
export class StatusTimeline {
  protected readonly meta = inject(MetaService);
  readonly entries = input.required<StatusHistoryEntry[]>();
  /** Show which application each entry belongs to (company page). */
  readonly showTitle = input(false);

  protected color(entry: StatusHistoryEntry): string {
    return statusColor(entry.toStatus, this.meta.phaseOf(entry.toStatus));
  }
}
