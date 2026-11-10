import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, Injectable, inject } from '@angular/core';
import { DialogMeta, Dialogs } from '../core/dialogs';
import { Icon } from './icon';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel: string;
  danger?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog" aria-describedby="confirm-message">
      <header>
        <h2 id="confirm-title">{{ data.title }}</h2>
        <button type="button" class="btn btn-ghost btn-icon btn-sm" (click)="ref.close(false)" aria-label="Close">
          <app-icon name="x" />
        </button>
      </header>
      <div class="body">
        <p id="confirm-message" class="muted">{{ data.message }}</p>
      </div>
      <footer>
        <button type="button" class="btn" (click)="ref.close(false)">Cancel</button>
        <button type="button" class="btn" [class.btn-danger]="data.danger" [class.btn-primary]="!data.danger" (click)="ref.close(true)" cdkFocusInitial>
          {{ data.confirmLabel }}
        </button>
      </footer>
    </div>
  `,
})
export class ConfirmDialog {
  static readonly dialog: DialogMeta = { labelledBy: 'confirm-title', role: 'alertdialog' };

  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
  protected readonly data = inject<ConfirmOptions>(DIALOG_DATA);
}

/** Confirmation before destructive actions. Resolves true only on an explicit confirm. */
@Injectable({ providedIn: 'root' })
export class Confirm {
  private readonly dialogs = inject(Dialogs);

  async ask(options: ConfirmOptions): Promise<boolean> {
    return (await this.dialogs.result<boolean, ConfirmOptions>(ConfirmDialog, { data: options, width: '440px' })) === true;
  }
}
