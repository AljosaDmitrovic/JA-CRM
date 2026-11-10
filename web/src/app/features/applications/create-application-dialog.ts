import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApplicationsApi } from '../../core/api/applications.api';
import { VacanciesApi } from '../../core/api/vacancies.api';
import { applyServerErrors, clean, controlError, urlValidator } from '../../core/forms';
import { MetaService } from '../../core/meta.service';
import { ApplicationStatus, Priority, VacancyListItem } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { CompanyField, companyFieldForm, companyFieldValue } from '../../shared/company-field';
import { Icon } from '../../shared/icon';
import { StatusSelect } from '../../shared/status-select';

type Mode = 'new' | 'existing';

/**
 * Quick "Create application": either from a saved vacancy, or capturing a new vacancy (title,
 * company, URL) inline. Company and vacancy are created in the same request (one transaction).
 */
@Component({
  selector: 'app-create-application-dialog',
  imports: [ReactiveFormsModule, Icon, CompanyField, StatusSelect],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" aria-labelledby="create-app-title">
      <header>
        <h2 id="create-app-title">Create application</h2>
        <button type="button" class="btn btn-ghost btn-icon btn-sm" (click)="ref.close()" aria-label="Close"><app-icon name="x" /></button>
      </header>
      <div class="body">
        <div class="segmented mode" role="radiogroup" aria-label="Vacancy">
          <button type="button" role="radio" [attr.aria-checked]="mode() === 'new'" (click)="mode.set('new')">New vacancy</button>
          <button type="button" role="radio" [attr.aria-checked]="mode() === 'existing'" (click)="mode.set('existing')" [disabled]="!vacancies().length">
            Saved vacancy{{ vacancies().length ? ' (' + vacancies().length + ')' : '' }}
          </button>
        </div>
        @if (banner(); as message) {
          <div class="alert danger" role="alert"><app-icon name="circle-alert" />{{ message }}</div>
        }
        <div class="form-grid">
          @if (mode() === 'new') {
            <div class="field span-2">
              <label for="ca-title" class="required">Job title</label>
              <input id="ca-title" class="input" [formControl]="form.controls.title" autocomplete="off" cdkFocusInitial />
              @if (err('title', 'Job title'); as e) {
                <span class="error">{{ e }}</span>
              }
            </div>
            <div class="field span-2">
              <label for="ca-company" class="required">Company</label>
              <app-company-field [form]="form.controls.company" inputId="ca-company" />
            </div>
            <div class="field span-2">
              <label for="ca-url">Posting URL</label>
              <input id="ca-url" class="input" [formControl]="form.controls.url" placeholder="https://…" autocomplete="off" />
              @if (err('url', 'URL'); as e) {
                <span class="error">{{ e }}</span>
              }
              <span class="hint">You can add the full description and details on the vacancy later.</span>
            </div>
          } @else {
            <div class="field span-2">
              <label for="ca-vacancy" class="required">Vacancy</label>
              <select id="ca-vacancy" class="select" [formControl]="form.controls.vacancyId">
                <option [ngValue]="null" disabled>Choose a vacancy without an application…</option>
                @for (v of vacancies(); track v.id) {
                  <option [ngValue]="v.id">{{ v.title }} · {{ v.companyName }}</option>
                }
              </select>
              @if (err('vacancyId', 'Vacancy'); as e) {
                <span class="error">{{ e }}</span>
              }
            </div>
          }
          <div class="field">
            <label for="ca-status">Status</label>
            <app-status-select selectId="ca-status" [formControl]="form.controls.status" />
          </div>
          <div class="field">
            <label for="ca-priority">Priority</label>
            <select id="ca-priority" class="select" [formControl]="form.controls.priority">
              @for (o of meta.options('priorities'); track o.value) {
                <option [ngValue]="o.value">{{ o.label }}</option>
              }
            </select>
          </div>
        </div>
      </div>
      <footer>
        <button type="button" class="btn" (click)="ref.close()">Cancel</button>
        <button type="submit" class="btn btn-primary" [disabled]="saving()">
          @if (saving()) {
            <app-icon name="loader" />
          }
          Create application
        </button>
      </footer>
    </form>
  `,
  styles: `
    .mode {
      margin-bottom: var(--s-4);
    }
    .alert {
      margin-bottom: var(--s-4);
    }
  `,
})
export class CreateApplicationDialog implements OnInit {
  protected readonly ref = inject<DialogRef<number>>(DialogRef);
  protected readonly meta = inject(MetaService);
  private readonly api = inject(ApplicationsApi);
  private readonly vacanciesApi = inject(VacanciesApi);
  private readonly toasts = inject(ToastService);

  protected readonly mode = signal<Mode>('new');
  protected readonly vacancies = signal<VacancyListItem[]>([]);
  protected readonly saving = signal(false);
  protected readonly banner = signal<string | null>(null);

  protected readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    company: companyFieldForm(),
    url: new FormControl('', { nonNullable: true, validators: [urlValidator] }),
    vacancyId: new FormControl<number | null>(null),
    status: new FormControl<ApplicationStatus>('Interested', { nonNullable: true }),
    priority: new FormControl<Priority>('Medium', { nonNullable: true }),
  });

  async ngOnInit(): Promise<void> {
    try {
      const page = await this.vacanciesApi.list({ hasApplication: false, sort: 'discovered', dir: 'desc', pageSize: 200 });
      this.vacancies.set(page.items);
    } catch {
      // The "new vacancy" path still works without the list.
    }
  }

  protected err(name: keyof typeof this.form.controls, label: string): string | null {
    return controlError(this.form.controls[name], label);
  }

  protected async save(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    this.banner.set(null);
    const v = this.form.getRawValue();
    const isNew = this.mode() === 'new';
    if (isNew && !v.title.trim()) {
      this.form.controls.title.setErrors({ required: true });
    }
    const company = companyFieldValue(this.form.controls.company);
    if (isNew && company.companyId === null && !company.newCompany) {
      this.form.controls.company.controls.companyId.setErrors({ server: 'Choose a company or type a new one.' });
    }
    if (!isNew && v.vacancyId === null) {
      this.form.controls.vacancyId.setErrors({ required: true });
    }
    if (this.form.invalid) return;

    this.saving.set(true);
    try {
      const created = await this.api.create({
        vacancyId: isNew ? null : v.vacancyId,
        newVacancy: isNew ? { title: v.title.trim(), ...company, url: clean(v.url) } : null,
        status: v.status,
        priority: v.priority,
      });
      this.toasts.success('Application created.');
      this.ref.close(created.id);
    } catch (error) {
      const unmatched = applyServerErrors(this.form, error, {
        'newVacancy.title': 'title',
        'newVacancy.url': 'url',
        'newVacancy.companyId': 'company.companyId',
        'newVacancy.newCompany.name': 'company.newCompanyName',
      });
      this.banner.set(unmatched.join(' ') || null);
    } finally {
      this.saving.set(false);
    }
  }
}
