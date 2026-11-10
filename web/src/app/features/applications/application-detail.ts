import { ChangeDetectionStrategy, Component, computed, effect, inject, input, numberAttribute, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { ApplicationsApi } from '../../core/api/applications.api';
import { Dialogs } from '../../core/dialogs';
import { Loadable } from '../../core/loadable';
import { MetaService } from '../../core/meta.service';
import { ApplicationDetail, ApplicationListItem, ApplicationPhase, ApplicationStatus } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { SettingsStore } from '../../core/settings.store';
import { ShortcutsService } from '../../core/shortcuts.service';
import { ToastService } from '../../core/toast.service';
import { Confirm } from '../../shared/confirm-dialog';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { MarkdownView } from '../../shared/markdown';
import { AppDatePipe, AppTimestampPipe, TimeAgoPipe } from '../../shared/pipes';
import { PriorityIndicator } from '../../shared/priority';
import { SalaryView } from '../../shared/salary-view';
import { StatusBadge } from '../../shared/status-badge';
import { StatusTimeline } from '../../shared/status-timeline';
import { MoveMenu } from './move-menu';
import { StatusChangeData, StatusChangeDialog } from './status-change-dialog';

@Component({
  selector: 'app-application-detail',
  imports: [RouterLink, Icon, EmptyState, MarkdownView, SalaryView, StatusBadge, StatusTimeline, PriorityIndicator, MoveMenu, AppDatePipe, AppTimestampPipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './application-detail.html',
  styleUrl: './application-detail.scss',
})
export class ApplicationDetailPage {
  readonly id = input.required({ transform: numberAttribute });
  protected readonly meta = inject(MetaService);
  protected readonly settings = inject(SettingsStore);
  private readonly api = inject(ApplicationsApi);
  private readonly dialogs = inject(Dialogs);
  private readonly confirm = inject(Confirm);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  protected readonly application = new Loadable<ApplicationDetail>();
  protected readonly descriptionOpen = signal(false);

  protected readonly snapshotPlace = computed(() => {
    const s = this.application.value()?.snapshot;
    return s ? [s.city, s.country].filter(Boolean).join(', ') : '';
  });

  constructor() {
    effect(() => {
      const id = this.id();
      this.descriptionOpen.set(false);
      void this.application.load(() => this.api.get(id)).then((a) => a && this.title.setTitle(`${a.vacancyTitle} · ${a.companyName} · Job Search`));
    });
    inject(ShortcutsService).register([
      { keys: 'e', description: 'Edit application', group: 'Application', run: () => void this.router.navigate(['/applications', this.id(), 'edit']) },
      { keys: 's', description: 'Change status', group: 'Application', run: () => this.application.value() && void this.changeStatus(this.application.value()!) },
    ]);
  }

  protected phaseLabel(phase: ApplicationPhase): string {
    return this.meta.meta().phases.find((p) => p.value === phase)?.label ?? phase;
  }

  protected join(parts: (string | null | undefined)[], separator = ' · '): string {
    return parts.filter(Boolean).join(separator);
  }

  protected async changeStatus(a: ApplicationDetail, target?: ApplicationStatus): Promise<void> {
    const updated = await this.dialogs.result<ApplicationListItem, StatusChangeData>(StatusChangeDialog, {
      data: { applicationId: a.id, current: a.status, target },
      width: '480px',
    });
    if (updated) {
      this.toasts.success(`Moved to ${this.meta.statusLabel(updated.status)}.`);
      await this.application.load(() => this.api.get(a.id));
    }
  }

  protected async toggleArchive(a: ApplicationDetail): Promise<void> {
    try {
      await this.api.setArchived(a.id, !a.isArchived);
      this.toasts.success(a.isArchived ? 'Application restored to the board.' : `Archived. It keeps its status (${this.meta.statusLabel(a.status)}).`);
      await this.application.load(() => this.api.get(a.id));
    } catch (error) {
      this.toasts.error(problemMessage(error));
    }
  }

  protected async remove(a: ApplicationDetail): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Delete this application?',
      message: 'The application and its status history are removed permanently. The vacancy is kept. Archive instead if you only want it off the board.',
      confirmLabel: 'Delete application',
      danger: true,
    });
    if (!ok) return;
    try {
      await this.api.delete(a.id);
      this.toasts.success('Application deleted.');
      void this.router.navigateByUrl('/applications');
    } catch (error) {
      this.toasts.error(problemMessage(error));
    }
  }
}
