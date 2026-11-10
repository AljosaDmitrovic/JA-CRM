import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, inject, input, output, viewChild } from '@angular/core';
import { MetaService } from '../../core/meta.service';
import { ApplicationStatus } from '../../core/models';
import { Icon } from '../../shared/icon';
import { statusColor } from '../../shared/status-badge';

/**
 * "Move to…" menu: the keyboard-accessible alternative to dragging a card (arrow keys, Enter,
 * Escape). Statuses are grouped by phase; the current one is marked and disabled.
 */
@Component({
  selector: 'app-move-menu',
  imports: [CdkMenuTrigger, CdkMenu, CdkMenuItem, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="btn btn-ghost btn-sm"
      [class.btn-icon]="compact()"
      [cdkMenuTriggerFor]="menu"
      [attr.aria-label]="'Move ' + itemLabel() + ' to another status'"
      [title]="compact() ? 'Move to… (M)' : ''"
      (click)="$event.stopPropagation()"
      (keydown)="$event.stopPropagation()"
    >
      <app-icon name="move" [size]="14" />
      @if (!compact()) {
        <span>Move to…</span>
      }
    </button>
    <ng-template #menu>
      <div class="menu move-menu" cdkMenu [attr.aria-label]="'Move ' + itemLabel() + ' to'">
        @for (phase of meta.meta().phases; track phase.value; let first = $first) {
          @if (!first) {
            <div class="menu-separator" role="separator"></div>
          }
          <div class="menu-label" aria-hidden="true">{{ phase.label }}</div>
          @for (status of phase.statuses; track status) {
            <button
              type="button"
              class="menu-item"
              cdkMenuItem
              [disabled]="status === current()"
              [class.current]="status === current()"
              (cdkMenuItemTriggered)="move.emit(status)"
            >
              <span class="dot" [style.background]="color(status, phase.value)"></span>
              {{ meta.statusLabel(status) }}
              @if (status === current()) {
                <span class="end">Current</span>
              }
            </button>
          }
        }
      </div>
    </ng-template>
  `,
  styles: `
    :host {
      display: inline-flex;
    }
    .dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      flex: none;
    }
  `,
})
export class MoveMenu {
  protected readonly meta = inject(MetaService);
  readonly current = input.required<ApplicationStatus>();
  readonly itemLabel = input('application');
  readonly compact = input(true);
  readonly move = output<ApplicationStatus>();
  private readonly trigger = viewChild.required(CdkMenuTrigger);

  /** Opens the menu with focus on its first item (used by the "M" shortcut on a focused card). */
  open(): void {
    this.trigger().open();
    setTimeout(() => (document.querySelector('.move-menu .menu-item:not(:disabled)') as HTMLElement | null)?.focus());
  }

  protected color(status: ApplicationStatus, phase: string): string {
    return statusColor(status, phase);
  }
}
