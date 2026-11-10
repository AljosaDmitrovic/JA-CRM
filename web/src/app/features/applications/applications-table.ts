import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MetaService } from '../../core/meta.service';
import { ApplicationListItem, Paged } from '../../core/models';
import { Pager, SortHeader } from '../../shared/pager';
import { AppDatePipe, TimeAgoPipe } from '../../shared/pipes';
import { PriorityIndicator } from '../../shared/priority';
import { SalaryView } from '../../shared/salary-view';
import { StatusBadge } from '../../shared/status-badge';
import { MoveRequest } from './application-board';
import { MoveMenu } from './move-menu';

@Component({
  selector: 'app-applications-table',
  imports: [RouterLink, Pager, SortHeader, StatusBadge, PriorityIndicator, SalaryView, MoveMenu, AppDatePipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table class="table" [class.dim]="loading()">
        <thead>
          <tr>
            <th><app-sort-header key="title" [sort]="sort()" [dir]="dir()" (sortChange)="sortChange.emit($event)">Application</app-sort-header></th>
            <th><app-sort-header key="status" [sort]="sort()" [dir]="dir()" (sortChange)="sortChange.emit($event)">Status</app-sort-header></th>
            <th class="hide-sm"><app-sort-header key="priority" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="sortChange.emit($event)">Priority</app-sort-header></th>
            <th class="hide-md">Location</th>
            <th class="hide-sm"><app-sort-header key="salary" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="sortChange.emit($event)">Salary</app-sort-header></th>
            <th class="hide-md"><app-sort-header key="submitted" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="sortChange.emit($event)">Submitted</app-sort-header></th>
            <th class="hide-sm"><app-sort-header key="updated" firstDir="desc" [sort]="sort()" [dir]="dir()" (sortChange)="sortChange.emit($event)">Updated</app-sort-header></th>
            <th><span class="visually-hidden">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          @for (a of page().items; track a.id) {
            <tr class="clickable" [class.archived]="a.isArchived" (click)="open(a, $event)">
              <td>
                <a class="primary-cell" [routerLink]="['/applications', a.id]">{{ a.title }}</a>
                @if (a.isArchived) {
                  <span class="tag-flag">Archived</span>
                }
                <span class="secondary">{{ a.companyName }}</span>
              </td>
              <td class="nowrap">
                <app-status-badge [status]="a.status" />
                @if (a.isStale) {
                  <span class="tag-flag warn" title="No status change for a while">Stale</span>
                }
              </td>
              <td class="hide-sm"><app-priority [priority]="a.priority" [showLabel]="true" /></td>
              <td class="hide-md">
                {{ a.city ?? a.country }}
                <span class="secondary">{{ meta.label('workArrangements', a.workArrangement) }}</span>
              </td>
              <td class="hide-sm">
                <app-salary-view [range]="a.salary" [normalized]="a.normalizedSalary" />
                @if (a.salarySource === 'Offered') {
                  <span class="tag-flag">Offer</span>
                }
              </td>
              <td class="hide-md nowrap subtle">{{ a.submittedOn | appDate }}</td>
              <td class="hide-sm nowrap subtle">{{ a.updatedAt | timeAgo }}</td>
              <td class="num"><app-move-menu [current]="a.status" [itemLabel]="a.title" (move)="move.emit({ item: a, status: $event })" /></td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    <app-pager [page]="page().page" [pageSize]="page().pageSize" [total]="page().total" (pageChange)="pageChange.emit($event)" />
  `,
})
export class ApplicationsTable {
  protected readonly meta = inject(MetaService);
  private readonly router = inject(Router);
  readonly page = input.required<Paged<ApplicationListItem>>();
  readonly loading = input(false);
  readonly sort = input.required<string>();
  readonly dir = input.required<'asc' | 'desc'>();
  readonly sortChange = output<{ sort: string; dir: 'asc' | 'desc' }>();
  readonly pageChange = output<number>();
  readonly move = output<MoveRequest>();

  protected open(a: ApplicationListItem, event: MouseEvent): void {
    if ((event.target as HTMLElement).closest('a, button')) return;
    void this.router.navigate(['/applications', a.id]);
  }
}
