import { ChangeDetectionStrategy, Component, effect, inject, input, numberAttribute } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { CompaniesApi } from '../../core/api/companies.api';
import { Dialogs } from '../../core/dialogs';
import { Loadable } from '../../core/loadable';
import { MetaService } from '../../core/meta.service';
import { problemMessage } from '../../core/problem';
import { CompanyDetail } from '../../core/models';
import { ShortcutsService } from '../../core/shortcuts.service';
import { ToastService } from '../../core/toast.service';
import { Confirm } from '../../shared/confirm-dialog';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { MarkdownView } from '../../shared/markdown';
import { AppDatePipe, AppTimestampPipe, TimeAgoPipe } from '../../shared/pipes';
import { PriorityIndicator } from '../../shared/priority';
import { Rating } from '../../shared/rating';
import { StatusBadge } from '../../shared/status-badge';
import { StatusTimeline } from '../../shared/status-timeline';
import { CompanyFormDialog } from './company-form-dialog';

@Component({
  selector: 'app-company-detail',
  imports: [RouterLink, Icon, EmptyState, MarkdownView, Rating, StatusBadge, StatusTimeline, PriorityIndicator, AppDatePipe, AppTimestampPipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page narrow">
      @if (company.error(); as error) {
        <app-empty-state icon="circle-alert" tone="danger" title="Could not load the company" [message]="error">
          <a class="btn" routerLink="/companies">Back to companies</a>
        </app-empty-state>
      } @else if (company.value(); as c) {
        <header class="page-header">
          <div class="title-block">
            <div class="eyebrow"><a routerLink="/companies">Companies</a><app-icon name="chevron-right" [size]="12" /></div>
            <h1>
              {{ c.name }}
              @if (c.isArchived) {
                <span class="tag-flag">Archived</span>
              }
            </h1>
            <p class="subtitle">{{ join([c.industry, location(c)]) || 'No details yet' }}</p>
          </div>
          <div class="actions">
            <a class="btn" [routerLink]="['/vacancies/new']" [queryParams]="{ companyId: c.id }"><app-icon name="plus" />Add vacancy</a>
            <button type="button" class="btn" (click)="edit(c)"><app-icon name="pencil" />Edit<kbd>E</kbd></button>
            <button type="button" class="btn btn-ghost btn-icon" (click)="toggleArchive(c)" [attr.aria-label]="c.isArchived ? 'Unarchive' : 'Archive'" [title]="c.isArchived ? 'Unarchive' : 'Archive'">
              <app-icon [name]="c.isArchived ? 'archive-restore' : 'archive'" />
            </button>
            @if (!c.vacancies.length) {
              <button type="button" class="btn btn-ghost btn-icon" (click)="remove(c)" aria-label="Delete" title="Delete"><app-icon name="trash" /></button>
            }
          </div>
        </header>

        <dl class="props">
          <dt>Website</dt>
          <dd>@if (c.website) { <a class="link" [href]="c.website" target="_blank" rel="noopener noreferrer">{{ c.website }}</a> } @else { <span class="subtle">—</span> }</dd>
          <dt>Careers page</dt>
          <dd>@if (c.careersUrl) { <a class="link" [href]="c.careersUrl" target="_blank" rel="noopener noreferrer">{{ c.careersUrl }}</a> } @else { <span class="subtle">—</span> }</dd>
          <dt>Headquarters</dt>
          <dd>{{ c.headquarters || '—' }}</dd>
          <dt>Location</dt>
          <dd>{{ location(c) || '—' }}</dd>
          <dt>Size</dt>
          <dd>{{ c.size ? meta.label('companySizes', c.size) + ' employees' : '—' }}</dd>
          <dt>Rating</dt>
          <dd><app-rating [value]="c.rating" /></dd>
          <dt>Added</dt>
          <dd>{{ c.createdAt | appTimestamp }}</dd>
        </dl>

        <section class="section">
          <header><h2>Notes</h2></header>
          @if (c.notes) {
            <app-markdown [text]="c.notes" />
          } @else {
            <p class="subtle">No notes. <button type="button" class="btn btn-ghost btn-sm" (click)="edit(c)">Add notes</button></p>
          }
        </section>

        <section class="section">
          <header>
            <h2>Vacancies <span class="subtle">{{ c.vacancies.length }}</span></h2>
          </header>
          @if (c.vacancies.length) {
            <div class="table-wrap">
              <table class="table">
                <tbody>
                  @for (v of c.vacancies; track v.id) {
                    <tr [class.archived]="v.isArchived">
                      <td>
                        <a class="primary-cell" [routerLink]="['/vacancies', v.id]">{{ v.title }}</a>
                        @if (v.isArchived) {
                          <span class="tag-flag">Archived</span>
                        }
                        <span class="secondary">{{ join([meta.label('seniorities', v.seniority), meta.label('workArrangements', v.workArrangement), v.city]) }}</span>
                      </td>
                      <td class="nowrap subtle hide-sm">{{ v.applicationDeadline ? 'Due ' + (v.applicationDeadline | appDate) : '' }}</td>
                      <td class="num">
                        @if (v.applicationStatus && v.applicationId) {
                          <a [routerLink]="['/applications', v.applicationId]"><app-status-badge [status]="v.applicationStatus" /></a>
                        } @else {
                          <span class="subtle">Not applied</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="subtle">No vacancies saved for this company.</p>
          }
        </section>

        <section class="section">
          <header>
            <h2>Applications <span class="subtle">{{ c.applications.length }}</span></h2>
          </header>
          @if (c.applications.length) {
            <div class="table-wrap">
              <table class="table">
                <tbody>
                  @for (a of c.applications; track a.id) {
                    <tr [class.archived]="a.isArchived">
                      <td>
                        <a class="primary-cell" [routerLink]="['/applications', a.id]">{{ a.title }}</a>
                        @if (a.isArchived) {
                          <span class="tag-flag">Archived</span>
                        }
                      </td>
                      <td class="hide-sm"><app-priority [priority]="a.priority" /></td>
                      <td class="nowrap subtle hide-sm">Updated {{ a.updatedAt | timeAgo }}</td>
                      <td class="num"><app-status-badge [status]="a.status" /></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="subtle">No applications yet.</p>
          }
        </section>

        @if (c.history.length) {
          <section class="section">
            <header><h2>Status history</h2></header>
            <app-status-timeline [entries]="c.history" [showTitle]="true" />
          </section>
        }
      } @else {
        <div class="skeleton-table" aria-busy="true"><span class="skeleton"></span><span class="skeleton"></span><span class="skeleton"></span></div>
      }
    </div>
  `,
})
export class CompanyDetailPage {
  readonly id = input.required({ transform: numberAttribute });
  protected readonly meta = inject(MetaService);
  private readonly api = inject(CompaniesApi);
  private readonly dialogs = inject(Dialogs);
  private readonly confirm = inject(Confirm);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  protected readonly company = new Loadable<CompanyDetail>();

  constructor() {
    effect(() => {
      const id = this.id();
      void this.company.load(() => this.api.get(id)).then((c) => c && this.title.setTitle(`${c.name} · Job Search`));
    });
    inject(ShortcutsService).register([
      { keys: 'e', description: 'Edit company', group: 'Company', run: () => this.company.value() && this.edit(this.company.value()!) },
    ]);
  }

  protected join(parts: (string | null | undefined)[]): string {
    return parts.filter(Boolean).join(' · ');
  }

  protected location(c: CompanyDetail): string {
    return [c.city, c.country].filter(Boolean).join(', ');
  }

  protected async edit(company: CompanyDetail): Promise<void> {
    const updated = await this.dialogs.result<CompanyDetail>(CompanyFormDialog, { data: { company }, width: '640px' });
    if (updated) this.company.value.set(updated);
  }

  protected async toggleArchive(company: CompanyDetail): Promise<void> {
    try {
      await this.api.setArchived(company.id, !company.isArchived);
      this.toasts.success(company.isArchived ? 'Company restored.' : 'Company archived. Its vacancies and applications are unchanged.');
      await this.company.load(() => this.api.get(company.id));
    } catch (error) {
      this.toasts.error(problemMessage(error));
    }
  }

  protected async remove(company: CompanyDetail): Promise<void> {
    const ok = await this.confirm.ask({
      title: `Delete ${company.name}?`,
      message: 'The company is removed permanently. This cannot be undone.',
      confirmLabel: 'Delete company',
      danger: true,
    });
    if (!ok) return;
    try {
      await this.api.delete(company.id);
      this.toasts.success(`${company.name} deleted.`);
      void this.router.navigateByUrl('/companies');
    } catch (error) {
      this.toasts.error(problemMessage(error));
    }
  }
}
