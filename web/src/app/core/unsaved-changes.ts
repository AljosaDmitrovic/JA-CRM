import { DialogRef } from '@angular/cdk/dialog';
import { DestroyRef, inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { Confirm } from '../shared/confirm-dialog';

/** Implemented by pages with a form that should not be left with unsaved edits. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

const discard = { title: 'Discard your changes?', message: 'What you typed has not been saved.', confirmLabel: 'Discard', danger: true };

/** Asks before leaving a page whose form has unsaved edits. */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) =>
  !component.hasUnsavedChanges() || inject(Confirm).ask(discard);

/** Warns before a reload or closing the tab while a form is dirty. Call in an injection context. */
export function warnOnUnload(isDirty: () => boolean): void {
  const handler = (event: BeforeUnloadEvent) => {
    if (isDirty()) event.preventDefault();
  };
  window.addEventListener('beforeunload', handler);
  inject(DestroyRef).onDestroy(() => window.removeEventListener('beforeunload', handler));
}

/** Escape or a backdrop click closes a form dialog only after confirming when it is dirty. */
export function guardDialogClose(isDirty: () => boolean): void {
  const ref = inject(DialogRef);
  const confirm = inject(Confirm);
  ref.disableClose = true;
  let asking = false;
  const attempt = async () => {
    if (asking) return;
    if (!isDirty()) {
      ref.close();
      return;
    }
    asking = true;
    try {
      if (await confirm.ask(discard)) ref.close();
    } finally {
      asking = false;
    }
  };
  ref.backdropClick.subscribe(() => void attempt());
  ref.keydownEvents.subscribe((event) => {
    if (event.key === 'Escape' && !event.defaultPrevented) {
      event.preventDefault();
      void attempt();
    }
  });
}
