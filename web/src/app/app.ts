import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastService } from './core/toast.service';
import { Icon } from './shared/icon';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <router-outlet />
    <div class="toasts" aria-live="polite" aria-atomic="false">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [class]="toast.kind" [attr.role]="toast.kind === 'error' ? 'alert' : 'status'">
          <app-icon [name]="toast.kind === 'error' ? 'circle-alert' : toast.kind === 'success' ? 'check' : 'info'" />
          <span class="message">{{ toast.message }}</span>
          @if (toast.action; as action) {
            <button type="button" class="btn btn-sm btn-ghost" (click)="action.run(); toasts.dismiss(toast.id)">
              {{ action.label }}
            </button>
          }
          <button type="button" class="btn btn-ghost btn-icon btn-sm" (click)="toasts.dismiss(toast.id)" aria-label="Dismiss">
            <app-icon name="x" [size]="14" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .toasts {
      position: fixed;
      right: var(--s-4);
      bottom: var(--s-4);
      z-index: 2000;
      display: flex;
      flex-direction: column;
      gap: var(--s-2);
      width: min(380px, calc(100vw - 32px));
      pointer-events: none;
    }
    .toast {
      display: flex;
      align-items: center;
      gap: var(--s-2);
      min-height: 42px;
      padding: var(--s-1) var(--s-1) var(--s-1) var(--s-3);
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      background: var(--surface);
      box-shadow: var(--shadow-overlay);
      font-size: var(--text-base);
      pointer-events: auto;
      animation: pop-in 140ms ease;
    }
    .message {
      flex: 1;
    }
    .success app-icon:first-child {
      color: var(--success);
    }
    .error app-icon:first-child {
      color: var(--danger);
    }
    .info app-icon:first-child {
      color: var(--accent);
    }
    @media (max-width: 900px) {
      .toasts {
        right: var(--s-4);
        bottom: calc(68px + env(safe-area-inset-bottom));
      }
    }
  `,
})
export class App {
  protected readonly toasts = inject(ToastService);
}
