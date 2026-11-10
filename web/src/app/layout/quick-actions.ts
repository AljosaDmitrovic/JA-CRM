import { Injectable } from '@angular/core';
import { Shortcut } from '../core/shortcuts.service';
import { IconName } from '../shared/icon';

export interface QuickAction {
  id: string;
  label: string;
  icon: IconName;
  keys?: string;
  run: () => void;
}

/** The quick actions (add company, add vacancy, create application). Filled in with the
 * feature modules in phase 3. */
@Injectable({ providedIn: 'root' })
export class QuickActions {
  paletteActions(): QuickAction[] {
    return [];
  }

  shortcuts(): Shortcut[] {
    return [];
  }
}
