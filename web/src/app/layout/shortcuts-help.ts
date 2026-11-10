import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, Injectable, computed, inject } from '@angular/core';
import { DialogMeta, Dialogs } from '../core/dialogs';
import { ShortcutsService, displayKeys } from '../core/shortcuts.service';
import { Icon } from '../shared/icon';

@Component({
  selector: 'app-shortcuts-help',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog">
      <header>
        <h2 id="shortcuts-title">Keyboard shortcuts</h2>
        <button type="button" class="btn btn-ghost btn-icon btn-sm" (click)="ref.close()" aria-label="Close">
          <app-icon name="x" />
        </button>
      </header>
      <div class="body">
        @for (group of groups(); track group.name) {
          <section>
            <h3>{{ group.name }}</h3>
            <dl>
              @for (shortcut of group.items; track shortcut.keys) {
                <dt>{{ shortcut.description }}</dt>
                <dd>
                  @for (part of shortcut.keys.split(' '); track $index; let last = $last) {
                    @for (key of display(part); track $index) {
                      <kbd>{{ key }}</kbd>
                    }
                    @if (!last) {
                      <span class="then">then</span>
                    }
                  }
                </dd>
              }
            </dl>
          </section>
        }
        <section>
          <h3>Boards and lists</h3>
          <dl>
            <dt>Move a focused card</dt>
            <dd><kbd>M</kbd></dd>
            <dt>Close a dialog or menu</dt>
            <dd><kbd>Esc</kbd></dd>
          </dl>
        </section>
      </div>
    </div>
  `,
  styles: `
    section + section {
      margin-top: var(--s-5);
    }
    h3 {
      margin-bottom: var(--s-2);
      color: var(--text-subtle);
      font-size: var(--text-xs);
      font-weight: 500;
    }
    dl {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: var(--s-2) var(--s-4);
      margin: 0;
    }
    dd {
      display: flex;
      align-items: center;
      gap: 3px;
      margin: 0;
    }
    .then {
      margin: 0 2px;
      color: var(--text-subtle);
      font-size: var(--text-xs);
    }
  `,
})
export class ShortcutsHelp {
  static readonly dialog: DialogMeta = { labelledBy: 'shortcuts-title' };

  protected readonly ref = inject(DialogRef);
  private readonly shortcuts = inject(ShortcutsService);

  protected readonly groups = computed(() => {
    const groups = new Map<string, { keys: string; description: string }[]>();
    for (const shortcut of this.shortcuts.active()) {
      groups.set(shortcut.group, [...(groups.get(shortcut.group) ?? []), shortcut]);
    }
    return [...groups].map(([name, items]) => ({ name, items }));
  });

  protected display(part: string): string[] {
    return displayKeys(part);
  }
}

@Injectable({ providedIn: 'root' })
export class ShortcutsHelpLauncher {
  private readonly dialogs = inject(Dialogs);
  private open_ = false;

  open(): void {
    if (this.open_) {
      return;
    }
    this.open_ = true;
    this.dialogs.open(ShortcutsHelp, { width: '480px' }).closed.subscribe(() => (this.open_ = false));
  }
}
