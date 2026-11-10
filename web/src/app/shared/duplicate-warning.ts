import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CompaniesApi } from '../core/api/companies.api';
import { DuplicateCompany } from '../core/models';
import { Icon } from './icon';

/** Warns (never blocks) when a likely duplicate company exists: same normalized name or domain. */
@Component({
  selector: 'app-duplicate-warning',
  imports: [Icon, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (matches().length) {
      <div class="alert" role="status">
        <app-icon name="warning" />
        <div>
          <strong>Possible duplicate.</strong>
          @for (match of matches(); track match.id; let last = $last) {
            <a class="link" [routerLink]="['/companies', match.id]" target="_blank">{{ match.name }}</a>
            <span class="subtle">
              ({{ match.reason === 'Domain' ? 'same website' : 'same name' }}{{ match.isArchived ? ', archived' : '' }})</span
            >{{ last ? '' : ', ' }}
          }
          <span class="muted"> already exists. You can still save.</span>
        </div>
      </div>
    }
  `,
})
export class DuplicateWarning {
  private readonly api = inject(CompaniesApi);
  readonly name = input<string | null>('');
  readonly website = input<string | null>(null);
  readonly excludeId = input<number | null>(null);
  protected readonly matches = signal<DuplicateCompany[]>([]);
  private timer: ReturnType<typeof setTimeout> | undefined;
  private sequence = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
    effect(() => {
      const name = (this.name() ?? '').trim();
      const website = (this.website() ?? '').trim() || null;
      const exclude = this.excludeId();
      clearTimeout(this.timer);
      if (!name && !website) {
        this.matches.set([]);
        return;
      }
      const current = ++this.sequence;
      this.timer = setTimeout(async () => {
        try {
          const matches = await this.api.duplicates(name, website, exclude);
          if (current === this.sequence) this.matches.set(matches);
        } catch {
          // A failed check must never block saving.
        }
      }, 300);
    });
  }
}
