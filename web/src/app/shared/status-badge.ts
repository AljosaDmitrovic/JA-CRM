import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MetaService } from '../core/meta.service';
import { ApplicationStatus } from '../core/models';

/** CSS colour token for a status: its phase colour, with Rejected marked separately. */
export function statusColor(status: ApplicationStatus, phase: string): string {
  if (status === 'Rejected') return 'var(--phase-rejected)';
  return `var(--phase-${phase.toLowerCase()})`;
}

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="dot" [style.background]="color()"></span>{{ label() }}`,
  host: { class: 'status-badge' },
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      height: 22px;
      padding: 0 8px;
      border: 1px solid var(--border);
      border-radius: 999px;
      background: var(--surface);
      color: var(--text);
      font-size: var(--text-sm);
      font-weight: 500;
      white-space: nowrap;
    }
    .dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      flex: none;
    }
  `,
})
export class StatusBadge {
  private readonly meta = inject(MetaService);
  readonly status = input.required<ApplicationStatus>();
  protected readonly label = computed(() => this.meta.statusLabel(this.status()));
  protected readonly color = computed(() => statusColor(this.status(), this.meta.phaseOf(this.status())));
}
