import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
  action?: { label: string; run: () => void };
}

/** Success and error notifications. Rendered by the shell in an aria-live region. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  success(message: string, action?: Toast['action']): void {
    this.show('success', message, action);
  }

  error(message: string): void {
    this.show('error', message, undefined, 7000);
  }

  info(message: string): void {
    this.show('info', message);
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(kind: ToastKind, message: string, action?: Toast['action'], timeout = 4000): void {
    const id = this.nextId++;
    // Identical messages replace each other instead of stacking (e.g. repeated network errors).
    this.toasts.update((list) => [...list.filter((t) => t.message !== message).slice(-3), { id, kind, message, action }]);
    setTimeout(() => this.dismiss(id), timeout);
  }
}
