import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Dialogs } from '../core/dialogs';
import { CompanyDetail } from '../core/models';
import { Shortcut } from '../core/shortcuts.service';
import { IconName } from '../shared/icon';

export interface QuickAction {
  id: string;
  label: string;
  icon: IconName;
  keys?: string;
  run: () => void;
}

/** Add company, add vacancy, create application: available from the dashboard, the command
 * palette and global shortcuts (N then C / V / A). Dialogs load on first use. */
@Injectable({ providedIn: 'root' })
export class QuickActions {
  private readonly dialogs = inject(Dialogs);
  private readonly router = inject(Router);

  private readonly actions: QuickAction[] = [
    { id: 'add-company', label: 'Add company', icon: 'building', keys: 'n c', run: () => void this.addCompany() },
    { id: 'add-vacancy', label: 'Add vacancy', icon: 'briefcase', keys: 'n v', run: () => void this.router.navigateByUrl('/vacancies/new') },
    { id: 'create-application', label: 'Create application', icon: 'plus', keys: 'n a', run: () => void this.createApplication() },
  ];

  paletteActions(): QuickAction[] {
    return this.actions;
  }

  shortcuts(): Shortcut[] {
    return this.actions.map((a) => ({ keys: a.keys!, description: a.label, group: 'Create', run: a.run }));
  }

  async addCompany(): Promise<void> {
    const { CompanyFormDialog } = await import('../features/companies/company-form-dialog');
    const company = await this.dialogs.result<CompanyDetail>(CompanyFormDialog, { width: '640px' });
    if (company) void this.router.navigate(['/companies', company.id]);
  }

  async createApplication(): Promise<void> {
    const { CreateApplicationDialog } = await import('../features/applications/create-application-dialog');
    const id = await this.dialogs.result<number>(CreateApplicationDialog, { width: '560px' });
    if (id) void this.router.navigate(['/applications', id]);
  }
}
