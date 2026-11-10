import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { comparabilityNote, normalizedText, salaryRangeText, salaryValueText } from '../core/format';
import { NormalizedSalary, SalaryRange, SalaryValue } from '../core/models';
import { Icon } from './icon';

/** A salary as entered, plus its annual value in the base currency. Net, unknown and
 * unconvertible amounts are flagged as not comparable (never estimated). */
@Component({
  selector: 'app-salary-view',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (text()) {
      <span class="original">{{ text() }}</span>
      @if (showNormalized() && (annual() || note())) {
        <span class="normalized" [class.flag]="!!note()">
          @if (annual()) {
            ≈ {{ annual() }}
          }
          @if (note(); as n) {
            <span class="note" [title]="n"><app-icon name="warning" [size]="12" />{{ short() }}</span>
          }
        </span>
      }
    } @else {
      <span class="subtle">{{ empty() }}</span>
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      flex-direction: column;
      gap: 1px;
      min-width: 0;
    }
    :host(.inline) {
      flex-direction: row;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 6px;
    }
    .normalized {
      color: var(--text-muted);
      font-size: var(--text-sm);
      font-variant-numeric: tabular-nums;
    }
    .note {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      margin-left: 4px;
      color: var(--warning);
      font-size: var(--text-xs);
    }
  `,
})
export class SalaryView {
  readonly value = input<SalaryValue | null>(null);
  readonly range = input<SalaryRange | null>(null);
  readonly normalized = input<NormalizedSalary | null>(null);
  readonly showNormalized = input(true);
  readonly empty = input('—');

  protected readonly text = computed(() => (this.range() ? salaryRangeText(this.range()) : salaryValueText(this.value())));
  protected readonly annual = computed(() => normalizedText(this.normalized()));
  protected readonly note = computed(() => comparabilityNote(this.normalized()));
  protected readonly short = computed(() => {
    switch (this.normalized()?.comparability) {
      case 'Net':
        return 'net, not comparable';
      case 'UnknownType':
        return 'type unknown';
      case 'MissingRate':
        return 'no exchange rate';
      default:
        return '';
    }
  });
}
