import { ChangeDetectionStrategy, Component, OnInit, inject, input, numberAttribute, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LookupsApi } from '../../core/api/misc.api';
import { VacanciesApi } from '../../core/api/vacancies.api';
import { applyServerErrors, clean, controlError, urlValidator } from '../../core/forms';
import { todayIso } from '../../core/format';
import { MetaService } from '../../core/meta.service';
import { EmploymentType, Lookups, Seniority, TriState, VacancyDetail, VacancyRequest, VacancySource, WorkArrangement } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { SettingsStore } from '../../core/settings.store';
import { ShortcutsService, isMac } from '../../core/shortcuts.service';
import { ToastService } from '../../core/toast.service';
import { HasUnsavedChanges, warnOnUnload } from '../../core/unsaved-changes';
import { CompanyField, companyFieldForm, companyFieldValue } from '../../shared/company-field';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { MarkdownView } from '../../shared/markdown';
import { SalaryFields, salaryRangeForm, toSalaryRange } from '../../shared/salary-fields';
import { TagInput } from '../../shared/tag-input';

/** Create or edit a vacancy: the full posting, pasted job description included. */
@Component({
  selector: 'app-vacancy-form',
  imports: [ReactiveFormsModule, RouterLink, Icon, EmptyState, CompanyField, SalaryFields, TagInput, MarkdownView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './vacancy-form.html',
  styles: `
    .description-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .preview {
      min-height: 160px;
      max-height: 560px;
      overflow-y: auto;
      padding: var(--s-3) var(--s-4);
      border: 1px solid var(--border);
      border-radius: var(--radius);
    }
    .sticky-actions {
      position: sticky;
      bottom: 0;
      z-index: 5;
      margin: 0 calc(var(--s-2) * -1);
      padding: var(--s-3) var(--s-2);
      background: color-mix(in srgb, var(--bg) 94%, transparent);
      backdrop-filter: blur(6px);
    }
    .tri {
      display: flex;
    }
  `,
})
export class VacancyFormPage implements OnInit, HasUnsavedChanges {
  /** Route param on /vacancies/:id/edit; absent on /vacancies/new. */
  readonly id = input(undefined, { transform: (v: unknown) => (v === undefined ? undefined : numberAttribute(v)) });

  protected readonly meta = inject(MetaService);
  private readonly api = inject(VacanciesApi);
  private readonly lookupsApi = inject(LookupsApi);
  private readonly settings = inject(SettingsStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toasts = inject(ToastService);

  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly banner = signal<string | null>(null);
  protected readonly preview = signal(false);
  protected readonly lookups = signal<Lookups | null>(null);
  protected readonly ready = signal(false);
  protected readonly saveKeys = isMac ? '⌘↵' : 'Ctrl ↵';

  protected readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    company: companyFieldForm(),
    seniority: new FormControl<Seniority | null>(null),
    employmentType: new FormControl<EmploymentType | null>(null),
    url: new FormControl('', { nonNullable: true, validators: [urlValidator] }),
    source: new FormControl<VacancySource | null>(null),
    sourceDetail: new FormControl('', { nonNullable: true }),
    dateDiscovered: new FormControl(todayIso(), { nonNullable: true }),
    applicationDeadline: new FormControl('', { nonNullable: true }),
    country: new FormControl('', { nonNullable: true }),
    city: new FormControl('', { nonNullable: true }),
    workArrangement: new FormControl<WorkArrangement | null>(null),
    remoteEligibility: new FormControl('', { nonNullable: true }),
    openToMyLocation: new FormControl<TriState>('Unknown', { nonNullable: true }),
    relocationSupport: new FormControl<TriState>('Unknown', { nonNullable: true }),
    relocationNotes: new FormControl('', { nonNullable: true }),
    requiredTechnologies: new FormControl<string[]>([], { nonNullable: true }),
    preferredTechnologies: new FormControl<string[]>([], { nonNullable: true }),
    requiredExperienceYears: new FormControl<number | null>(null, [Validators.min(0), Validators.max(60)]),
    requiredExperienceText: new FormControl('', { nonNullable: true }),
    postedSalary: salaryRangeForm(null, this.settings.baseCurrency()),
    description: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true }),
  });

  private saved = false;

  constructor() {
    warnOnUnload(() => this.hasUnsavedChanges());
    inject(ShortcutsService).register([
      { keys: 'mod+Enter', description: 'Save vacancy', group: 'Vacancy form', run: () => void this.save(), allowInInputs: true },
    ]);
  }

  protected get editing(): boolean {
    return this.id() !== undefined;
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saved;
  }

  async ngOnInit(): Promise<void> {
    void this.lookupsApi.get().then((l) => this.lookups.set(l), () => undefined);
    const id = this.id();
    if (id === undefined) {
      const companyId = Number(this.route.snapshot.queryParamMap.get('companyId'));
      if (companyId) this.form.controls.company.setValue({ companyId, newCompanyName: '' });
      this.ready.set(true);
      return;
    }
    this.loading.set(true);
    try {
      this.patch(await this.api.get(id));
      this.ready.set(true);
    } catch (error) {
      this.loadError.set(problemMessage(error, 'Could not load the vacancy.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected err(path: string, label: string): string | null {
    return controlError(this.form.get(path), label);
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    const company = companyFieldValue(this.form.controls.company);
    if (company.companyId === null && !company.newCompany) {
      this.form.controls.company.controls.companyId.setErrors({ server: 'Choose a company or type a new one.' });
    }
    if (this.form.invalid || this.saving()) {
      this.banner.set('Check the highlighted fields.');
      return;
    }
    this.saving.set(true);
    this.banner.set(null);
    const v = this.form.getRawValue();
    const request: VacancyRequest = {
      title: v.title.trim(),
      ...company,
      seniority: v.seniority,
      description: clean(v.description),
      url: clean(v.url),
      source: v.source,
      sourceDetail: clean(v.sourceDetail),
      country: clean(v.country),
      city: clean(v.city),
      workArrangement: v.workArrangement,
      remoteEligibility: clean(v.remoteEligibility),
      openToMyLocation: v.openToMyLocation,
      relocationSupport: v.relocationSupport,
      relocationNotes: clean(v.relocationNotes),
      employmentType: v.employmentType,
      requiredTechnologies: v.requiredTechnologies,
      preferredTechnologies: v.preferredTechnologies,
      requiredExperienceYears: v.requiredExperienceYears,
      requiredExperienceText: clean(v.requiredExperienceText),
      postedSalary: toSalaryRange(this.form.controls.postedSalary),
      dateDiscovered: v.dateDiscovered || null,
      applicationDeadline: v.applicationDeadline || null,
      notes: clean(v.notes),
    };
    try {
      const id = this.id();
      const saved = id === undefined ? await this.api.create(request) : await this.api.update(id, request);
      this.saved = true;
      this.toasts.success(id === undefined ? 'Vacancy saved.' : 'Changes saved.');
      void this.router.navigate(['/vacancies', saved.id]);
    } catch (error) {
      const unmatched = applyServerErrors(this.form, error, {
        companyId: 'company.companyId',
        'newCompany.name': 'company.newCompanyName',
      });
      this.banner.set(unmatched.join(' ') || 'Check the highlighted fields.');
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    const id = this.id();
    void this.router.navigateByUrl(id === undefined ? '/vacancies' : `/vacancies/${id}`);
  }

  private patch(v: VacancyDetail): void {
    this.form.patchValue({
      title: v.title,
      company: { companyId: v.companyId, newCompanyName: '' },
      seniority: v.seniority,
      employmentType: v.employmentType,
      url: v.url ?? '',
      source: v.source,
      sourceDetail: v.sourceDetail ?? '',
      dateDiscovered: v.dateDiscovered ?? '',
      applicationDeadline: v.applicationDeadline ?? '',
      country: v.country ?? '',
      city: v.city ?? '',
      workArrangement: v.workArrangement,
      remoteEligibility: v.remoteEligibility ?? '',
      openToMyLocation: v.openToMyLocation,
      relocationSupport: v.relocationSupport,
      relocationNotes: v.relocationNotes ?? '',
      requiredTechnologies: v.requiredTechnologies,
      preferredTechnologies: v.preferredTechnologies,
      requiredExperienceYears: v.requiredExperienceYears,
      requiredExperienceText: v.requiredExperienceText ?? '',
      description: v.description ?? '',
      notes: v.notes ?? '',
    });
    this.form.setControl('postedSalary', salaryRangeForm(v.postedSalary, this.settings.baseCurrency()));
  }
}
