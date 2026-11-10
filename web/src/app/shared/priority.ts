import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Priority } from '../core/models';

/** Three bars, filled by priority (Linear-style); the label is available to screen readers. */
@Component({
  selector: 'app-priority',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="bars" aria-hidden="true">
      <i [class.on]="level() >= 1"></i><i [class.on]="level() >= 2"></i><i [class.on]="level() >= 3"></i>
    </span>
    @if (showLabel()) {
      <span class="label">{{ priority() }}</span>
    } @else {
      <span class="visually-hidden">{{ priority() }} priority</span>
    }
  `,
  host: { '[attr.title]': 'priority() + " priority"', '[class.high]': "priority() === 'High'" },
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      color: var(--text-muted);
      font-size: var(--text-sm);
    }
    .bars {
      display: inline-flex;
      align-items: flex-end;
      gap: 1.5px;
      height: 11px;
    }
    i {
      width: 3px;
      border-radius: 1px;
      background: var(--border-strong);
    }
    i:nth-child(1) { height: 5px; }
    i:nth-child(2) { height: 8px; }
    i:nth-child(3) { height: 11px; }
    i.on {
      background: var(--text-muted);
    }
    :host(.high) i.on {
      background: var(--text);
    }
  `,
})
export class PriorityIndicator {
  readonly priority = input.required<Priority>();
  readonly showLabel = input(false);
  protected readonly level = computed(() => ({ Low: 1, Medium: 2, High: 3 })[this.priority()]);
}
