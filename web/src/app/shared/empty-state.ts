import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon, IconName } from './icon';

/** Centered empty or error state with an optional call to action (projected content). */
@Component({
  selector: 'app-empty-state',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="icon" [class.danger]="tone() === 'danger'"><app-icon [name]="icon()" [size]="20" /></div>
    <h3>{{ title() }}</h3>
    @if (message()) {
      <p>{{ message() }}</p>
    }
    <div class="actions"><ng-content /></div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--s-2);
      padding: var(--s-12) var(--s-4);
      text-align: center;
    }
    :host(.compact) {
      padding: var(--s-6) var(--s-4);
    }
    .icon {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      margin-bottom: var(--s-1);
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      color: var(--text-subtle);
      background: var(--surface);
    }
    .icon.danger {
      color: var(--danger);
    }
    h3 {
      font-size: var(--text-md);
      font-weight: 600;
    }
    p {
      max-width: 380px;
      color: var(--text-muted);
    }
    .actions:not(:empty) {
      display: flex;
      gap: var(--s-2);
      margin-top: var(--s-3);
    }
  `,
})
export class EmptyState {
  readonly icon = input<IconName>('inbox');
  readonly title = input.required<string>();
  readonly message = input<string>();
  readonly tone = input<'default' | 'danger'>('default');
}
