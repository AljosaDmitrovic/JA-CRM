import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApplicationsApi } from '../../core/api/applications.api';
import { DashboardApi } from '../../core/api/misc.api';
import { Loadable } from '../../core/loadable';
import { MetaService } from '../../core/meta.service';
import { ApplicationPhase, Dashboard } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { QuickActions } from '../../layout/quick-actions';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { AppDatePipe, TimeAgoPipe } from '../../shared/pipes';
import { StatusBadge, statusColor } from '../../shared/status-badge';

interface Tile {
  label: string;
  value: number;
  hint: string;
  link: string;
  query: Record<string, string | string[]>;
}

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, Icon, EmptyState, StatusBadge, AppDatePipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardPage {
  protected readonly meta = inject(MetaService);
  protected readonly quick = inject(QuickActions);
  private readonly api = inject(DashboardApi);
  private readonly applications = inject(ApplicationsApi);
  private readonly toasts = inject(ToastService);
  protected readonly dashboard = new Loadable<Dashboard>();

  protected readonly tiles = computed<Tile[]>(() => {
    const m = this.dashboard.value()?.metrics;
    if (!m) return [];
    const table = { view: 'table' };
    return [
      { label: 'Total applications', value: m.totalApplications, hint: 'All time, archived included', link: '/applications', query: { ...table, archived: 'All' } },
      { label: 'Active', value: m.activeApplications, hint: 'Applied, interviewing or offer', link: '/applications', query: { ...table, phase: ['Applied', 'Interviewing', 'Offer'] } },
      { label: 'Interviews in progress', value: m.interviewsInProgress, hint: 'Any interview stage', link: '/applications', query: { ...table, phase: 'Interviewing' } },
      { label: 'Offers received', value: m.offersReceived, hint: 'Ever reached Offer Received', link: '/applications', query: { ...table, archived: 'All', phase: 'Offer' } },
      { label: 'Rejected', value: m.rejectedApplications, hint: 'Archived included', link: '/applications', query: { ...table, archived: 'All', status: 'Rejected' } },
      { label: 'Awaiting response', value: m.awaitingResponse, hint: m.staleApplications ? `${m.staleApplications} stale` : 'Applied, no reply yet', link: '/applications', query: { ...table, status: 'Applied' } },
    ];
  });

  /** One segmented bar: non-archived applications by phase. Parts at zero draw nothing. */
  protected readonly pipeline = computed(() => {
    const d = this.dashboard.value();
    if (!d) return null;
    const total = d.pipeline.reduce((sum, p) => sum + p.count, 0);
    return {
      total,
      phases: d.pipeline.map((p) => ({
        phase: p.phase,
        label: this.phaseLabel(p.phase),
        count: p.count,
        share: total ? p.count / total : 0,
        color: `var(--phase-${p.phase.toLowerCase()})`,
        statuses: p.statuses.map((s) => ({ ...s, label: this.meta.statusLabel(s.status), color: statusColor(s.status, p.phase) })),
      })),
    };
  });

  protected readonly isEmpty = computed(() => {
    const d = this.dashboard.value();
    return !!d && d.metrics.totalApplications === 0 && d.upcomingDeadlines.length === 0;
  });

  constructor() {
    void this.reload();
  }

  protected reload(): Promise<unknown> {
    return this.dashboard.load(() => this.api.get());
  }

  protected phaseLabel(phase: ApplicationPhase): string {
    return this.meta.meta().phases.find((p) => p.value === phase)?.label ?? phase;
  }

  protected percent(share: number): string {
    return `${Math.round(share * 100)}%`;
  }

  protected deadlineText(daysLeft: number): string {
    return daysLeft === 0 ? 'Today' : daysLeft === 1 ? 'Tomorrow' : `In ${daysLeft} days`;
  }

  /** Stale applications are the candidates for No Response; the move is always manual. */
  protected async markNoResponse(id: number): Promise<void> {
    try {
      await this.applications.changeStatus(id, 'NoResponse', 'Marked as No Response from the dashboard (stale).');
      this.toasts.success('Moved to No Response.');
      await this.reload();
    } catch (error) {
      this.toasts.error(problemMessage(error));
    }
  }
}
