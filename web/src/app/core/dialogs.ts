import { Dialog, DialogRef } from '@angular/cdk/dialog';
import { Overlay } from '@angular/cdk/overlay';
import { ComponentType } from '@angular/cdk/portal';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ShortcutsService } from './shortcuts.service';

/** Thin wrapper over CDK Dialog: consistent panel styling, and single-key shortcuts are
 * suspended while a dialog is open. */
@Injectable({ providedIn: 'root' })
export class Dialogs {
  private readonly dialog = inject(Dialog);
  private readonly overlay = inject(Overlay);
  private readonly shortcuts = inject(ShortcutsService);

  open<TResult, TData = unknown, TComponent = unknown>(
    component: ComponentType<TComponent>,
    options: { data?: TData; width?: string; position?: 'center' | 'top' } = {},
  ): DialogRef<TResult, TComponent> {
    const ref = this.dialog.open<TResult, TData, TComponent>(component, {
      data: options.data,
      width: options.width ?? '520px',
      panelClass: 'app-dialog-panel',
      backdropClass: ['cdk-overlay-dark-backdrop', 'app-backdrop'],
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      positionStrategy:
        options.position === 'top'
          ? this.overlay.position().global().centerHorizontally().top('12vh')
          : undefined,
    });
    const resume = this.shortcuts.suspend();
    ref.closed.subscribe(() => resume());
    return ref;
  }

  /** Opens a dialog and resolves with its result (undefined when dismissed). */
  result<TResult, TData = unknown, TComponent = unknown>(
    component: ComponentType<TComponent>,
    options: { data?: TData; width?: string; position?: 'center' | 'top' } = {},
  ): Promise<TResult | undefined> {
    return firstValueFrom(this.open<TResult, TData, TComponent>(component, options).closed);
  }
}
