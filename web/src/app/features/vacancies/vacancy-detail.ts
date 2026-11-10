import { ChangeDetectionStrategy, Component, effect, inject, input, numberAttribute } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { VacanciesApi } from '../../core/api/vacancies.api';
import { Dialogs } from '../../core/dialogs';
import { Loadable } from '../../core/loadable';
import { MetaService } from '../../core/meta.service';
import { VacancyDetail } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ShortcutsService } from '../../core/shortcuts.service';
import { ToastService } from '../../core/toast.service';
import { Confirm } from '../../shared/confirm-dialog';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { MarkdownView } from '../../shared/markdown';
import { AppDatePipe, AppTimestampPipe } from '../../shared/pipes';
import { SalaryView } from '../../shared/salary-view';
import { StatusBadge } from '../../shared/status-badge';
import { ConvertData, ConvertDialog } from './convert-dialog';

@Component({
  selector: 'app-vacancy-detail',
  imports: [RouterLink, Icon, EmptyState, MarkdownView, SalaryView, StatusBadge, AppDatePipe, AppTimestampPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page narrow">
      @if (vacancy.error(); as error) {
        <app-empty-state icon="circle-alert" tone="danger" title="Could not load the vacancy" [message]="error">
          <a class="btn" routerLink="/vacancies">Back to vacancies</a>
        </app-empty-state>
      } @else if (vacancy.value(); as v) {
        <header class="page-header">
          <div class="title-block">
            <div class="eyebrow">
              <a routerLink="/vacancies">Vacancies</a><app-icon name="chevron-right" [size]="12" />
              <a [routerLink]="['/companies', v.companyId]">{{ v.companyName }}</a>
            </div>
            <h1>
              {{ v.title }}
              @if (v.isArchived) {
                <span class="tag-flag">Archived</span>
              }
            </h1>
            <p class="subtitle">{{ summary(v) }}</p>
          </div>
          <div class="actions">
            @if (v.applicationId && v.applicationStatus) {
              <a class="btn" [routerLink]="['/applications', v.applicationId]">Open application <app-status-badge [status]="v.applicationStatus" /></a>
            } @else {
              <button type="button" class="btn btn-primary" (click)="convert(v)"><app-icon name="arrow-right" />Convert to application</button>
            }
            <a class="btn" [routerLink]="['/vacancies', v.id, 'edit']"><app-icon name="pencil" />Edit<kbd>E</kbd></a>
            <button type="button" class="btn btn-ghost btn-icon" (click)="toggleArchive(v)" [attr.aria-label]="v.isArchived ? 'Unarchive' : 'Archive'" [title]="v.isArchived ? 'Unarchive' : 'Archive'">
              <app-icon [name]="v.isArchived ? 'archive-restore' : 'archive'" />
            </button>
            @if (!v.applicationId) {
              <button type="button" class="btn btn-ghost btn-icon" (click)="remove(v)" aria-label="Delete" title="Delete"><app-icon name="trash" /></button>
            }
          </div>
        </header>

        <dl class="props">
          <dt>Posted salary</dt>
          <dd><app-salary-view class="inline" [range]="v.postedSalary" [normalized]="v.normalizedSalary" empty="Not stated" /></dd>
          <dt>Location</dt>
          <dd>{{ join([v.city, v.country]) || '—' }}</dd>
          <dt>Work arrangement</dt>
          <dd>{{ meta.label('workArrangements', v.workArrangement) || '—' }}{{ v.remoteEligibility ? ' · ' + v.remoteEligibility : '' }}</dd>
          <dt>Open to my location</dt>
          <dd><span class="tag-flag" [class.warn]="v.openToMyLocation === 'No'">{{ v.openToMyLocation }}</span></dd>
          <dt>Relocation support</dt>
          <dd>{{ v.relocationSupport }}{{ v.relocationNotes ? ' · ' + v.relocationNotes : '' }}</dd>
          <dt>Employment</dt>
          <dd>{{ join([meta.label('employmentTypes', v.employmentType), meta.label('seniorities', v.seniority)]) || '—' }}</dd>
          <dt>Experience</dt>
          <dd>{{ join([v.requiredExperienceYears !== null ? v.requiredExperienceYears + '+ years' : null, v.requiredExperienceText]) || '—' }}</dd>
          <dt>Required</dt>
          <dd>
            <div class="chips">
              @for (t of v.requiredTechnologies; track t) {
                <span class="chip strong">{{ t }}</span>
              } @empty {
                <span class="subtle">—</span>
              }
            </div>
          </dd>
          <dt>Preferred</dt>
          <dd>
            <div class="chips">
              @for (t of v.preferredTechnologies; track t) {
                <span class="chip">{{ t }}</span>
              } @empty {
                <span class="subtle">—</span>
              }
            </div>
          </dd>
          <dt>Source</dt>
          <dd>{{ join([meta.label('sources', v.source), v.sourceDetail]) || '—' }}</dd>
          <dt>Posting</dt>
          <dd>@if (v.url) { <a class="link" [href]="v.url" target="_blank" rel="noopener noreferrer">{{ v.url }}</a> } @else { <span class="subtle">—</span> }</dd>
          <dt>Discovered</dt>
          <dd>{{ (v.dateDiscovered | appDate) || '—' }}</dd>
          <dt>Deadline</dt>
          <dd>{{ (v.applicationDeadline | appDate) || '—' }}</dd>
          <dt>Last updated</dt>
          <dd>{{ v.updatedAt | appTimestamp: 'time' }}</dd>
        </dl>

        <section class="section">
          <header><h2>Job description</h2></header>
          @if (v.description) {
            <app-markdown [text]="v.description" />
          } @else {
            <p class="subtle">No description saved. <a class="link" [routerLink]="['/vacancies', v.id, 'edit']">Paste it in</a>.</p>
          }
        </section>

        @if (v.notes) {
          <section class="section">
            <header><h2>Notes</h2></header>
            <app-markdown [text]="v.notes" />
          </section>
        }
      } @else {
        <div class="skeleton-table" aria-busy="true"><span class="skeleton"></span><span class="skeleton"></span><span class="skeleton"></span></div>
      }
    </div>
  `,
})
export class VacancyDetailPage {
  readonly id = input.required({ transform: numberAttribute });
  protected readonly meta = inject(MetaService);
  private readonly api = inject(VacanciesApi);
  private readonly dialogs = inject(Dialogs);
  private readonly confirm = inject(Confirm);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly vacancy = new Loadable<VacancyDetail>();

  constructor() {
    effect(() => {
      const id = this.id();
      void this.vacancy.load(() => this.api.get(id));
    });
    inject(ShortcutsService).register([
      { keys: 'e', description: 'Edit vacancy', group: 'Vacancy', run: () => void this.router.navigate(['/vacancies', this.id(), 'edit']) },
    ]);
  }

  protected join(parts: (string | null | undefined)[]): string {
    return parts.filter(Boolean).join(' · ');
  }

  protected summary(v: VacancyDetail): string {
    return this.join([this.meta.label('seniorities', v.seniority), this.meta.label('workArrangements', v.workArrangement), this.join([v.city, v.country])]);
  }

  protected async convert(v: VacancyDetail): Promise<void> {
    const id = await this.dialogs.result<number, ConvertData>(ConvertDialog, {
      data: { vacancyId: v.id, title: v.title, companyName: v.companyName },
      width: '480px',
    });
    if (id) void this.router.navigate(['/applications', id]);
  }

  protected async toggleArchive(v: VacancyDetail): Promise<void> {
    try {
      await this.api.setArchived(v.id, !v.isArchived);
      this.toasts.success(v.isArchived ? 'Vacancy restored.' : 'Vacancy archived.');
      await this.vacancy.load(() => this.api.get(v.id));
    } catch (error) {
      this.toasts.error(problemMessage(error));
    }
  }

  protected async remove(v: VacancyDetail): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Delete this vacancy?',
      message: `“${v.title}” is removed permanently. This cannot be undone.`,
      confirmLabel: 'Delete vacancy',
      danger: true,
    });
    if (!ok) return;
    try {
      await this.api.delete(v.id);
      this.toasts.success('Vacancy deleted.');
      void this.router.navigateByUrl('/vacancies');
    } catch (error) {
      this.toasts.error(problemMessage(error));
    }
  }
}
