import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth';
import { ShortcutsService, displayKeys } from '../core/shortcuts.service';
import { Icon, IconName } from '../shared/icon';
import { CommandPaletteLauncher } from './command-palette';
import { QuickActions } from './quick-actions';
import { ShortcutsHelpLauncher } from './shortcuts-help';

interface NavItem {
  path: string;
  label: string;
  icon: IconName;
  keys: string;
  exact?: boolean;
}

export const NAV_ITEMS: NavItem[] = [
  { path: '/', label: 'Dashboard', icon: 'dashboard', keys: 'g d', exact: true },
  { path: '/applications', label: 'Applications', icon: 'kanban', keys: 'g a' },
  { path: '/companies', label: 'Companies', icon: 'building', keys: 'g c' },
  { path: '/vacancies', label: 'Vacancies', icon: 'briefcase', keys: 'g v' },
  { path: '/settings', label: 'Settings', icon: 'settings', keys: 'g s' },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly palette = inject(CommandPaletteLauncher);
  protected readonly help = inject(ShortcutsHelpLauncher);
  protected readonly nav = NAV_ITEMS;
  protected readonly paletteKeys = displayKeys('mod+k');

  constructor() {
    const router = inject(Router);
    const quick = inject(QuickActions);
    inject(ShortcutsService).register([
      { keys: 'mod+k', description: 'Open command palette', group: 'General', run: () => this.palette.toggle(), allowInInputs: true },
      { keys: '?', description: 'Show keyboard shortcuts', group: 'General', run: () => this.help.open() },
      ...NAV_ITEMS.map((item) => ({
        keys: item.keys,
        description: `Go to ${item.label}`,
        group: 'Navigation',
        run: () => void router.navigateByUrl(item.path),
      })),
      ...quick.shortcuts(),
    ]);
  }
}
