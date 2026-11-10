import { Dialog, DialogRef } from '@angular/cdk/dialog';
import { Overlay } from '@angular/cdk/overlay';
import { ComponentType } from '@angular/cdk/portal';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ShortcutsService } from './shortcuts.service';

/** Accessible name and role of a dialog. A dialog component declares it once as a static
 * `dialog` property; the CDK container (the element with role="dialog") gets the attributes. */
export interface DialogMeta {
  /** Id of the visible title inside the dialog. */
  labelledBy?: string;
  /** Label when there is no visible title. */
  label?: string;
  /** 'alertdialog' for confirmations that interrupt the user. */
  role?: 'dialog' | 'alertdialog';
}

/** Thin wrapper over CDK Dialog: consistent panel styling, accessible names, and single-key
 * shortcuts are suspended while a dialog is open. */
@Injectable({ providedIn: 'root' })
export class Dialogs {
  private readonly dialog = inject(Dialog);
  private readonly overlay = inject(Overlay);
  private readonly shortcuts = inject(ShortcutsService);

  open<TResult, TData = unknown, TComponent = unknown>(
    component: ComponentType<TComponent>,
    options: { data?: TData; width?: string; position?: 'center' | 'top' } = {},
  ): DialogRef<TResult, TComponent> {
    const meta = (component as { dialog?: DialogMeta }).dialog ?? {};
    const ref = this.dialog.open<TResult, TData, TComponent>(component, {
      data: options.data,
      ariaLabelledBy: meta.labelledBy ?? null,
      ariaLabel: meta.label ?? null,
      role: meta.role ?? 'dialog',
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
