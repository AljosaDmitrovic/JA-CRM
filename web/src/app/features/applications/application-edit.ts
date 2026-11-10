import { ChangeDetectionStrategy, Component, OnInit, inject, input, numberAttribute, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApplicationsApi } from '../../core/api/applications.api';
import { applyServerErrors, clean, controlError } from '../../core/forms';
import { todayIso } from '../../core/format';
import { MetaService } from '../../core/meta.service';
import { ApplicationDetail, Priority } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { SettingsStore } from '../../core/settings.store';
import { ShortcutsService, isMac } from '../../core/shortcuts.service';
import { ToastService } from '../../core/toast.service';
import { HasUnsavedChanges, warnOnUnload } from '../../core/unsaved-changes';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { SalaryFields, salaryValueForm, toSalaryValue } from '../../shared/salary-fields';
import { StatusBadge } from '../../shared/status-badge';

/** Edits my pursuit of the vacancy. The posting itself is edited on the vacancy; the status
 * changes through the board or the "Change status" action (so it is always in the history). */
@Component({
  selector: 'app-application-edit',
  imports: [ReactiveFormsModule, RouterLink, Icon, EmptyState, SalaryFields, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page narrow">
      @if (loadError(); as error) {
        <app-empty-state icon="circle-alert" tone="danger" title="Could not load the application" [message]="error">
          <a class="btn" routerLink="/applications">Back to applications</a>
        </app-empty-state>
      } @else if (application(); as a) {
        <header class="page-header">
          <div class="title-block">
            <div class="eyebrow">
              <a routerLink="/applications">Applications</a><app-icon name="chevron-right" [size]="12" />
              <a [routerLink]="['/applications', a.id]">{{ a.vacancyTitle }}</a>
            </div>
            <h1>Edit application</h1>
            <p class="subtitle">{{ a.companyName }} · <app-status-badge [status]="a.status" /></p>
          </div>
        </header>

        <form [formGroup]="form" (ngSubmit)="save()" novalidate>
          <section class="form-section">
            <h2>Pursuit</h2>
            <div class="form-grid">
              <div class="field">
                <label for="ae-priority">Priority</label>
                <select id="ae-priority" class="select" formControlName="priority">
                  @for (o of meta.options('priorities'); track o.value) {
                    <option [ngValue]="o.value">{{ o.label }}</option>
                  }
                </select>
              </div>
              <div class="field">
                <label for="ae-submitted">Submission date</label>
                <input id="ae-submitted" class="input" type="date" formControlName="submittedOn" [max]="today" />
                <span class="hint">Filled in automatically when the application first reaches Applied or later.</span>
                @if (err('submittedOn', 'Submission date'); as e) {
                  <span class="error">{{ e }}</span>
                }
              </div>
              <div class="field span-2">
                <label for="ae-reason">Reason for applying</label>
                <textarea id="ae-reason" class="textarea" formControlName="reasonForApplying" rows="3"></textarea>
                @if (err('reasonForApplying', 'Reason'); as e) {
                  <span class="error">{{ e }}</span>
                }
              </div>
            </div>
          </section>

          <section class="form-section">
            <h2>Compensation</h2>
            <p class="section-hint">Full amounts with their terms. Leave the amount empty when unknown.</p>
            <div class="form-grid">
              <div class="field span-2">
                <label for="ae-expected-amount">Expected salary</label>
                <app-salary-fields [group]="form.controls.expectedSalary" label="Expected salary" idPrefix="ae-expected" />
              </div>
              <div class="field span-2">
                <label for="ae-offered-amount">Offered salary</label>
                <app-salary-fields [group]="form.controls.offeredSalary" label="Offered salary" idPrefix="ae-offered" />
              </div>
              <div class="field span-2">
                <label for="ae-comp-notes">Compensation notes</label>
                <textarea id="ae-comp-notes" class="textarea" formControlName="compensationNotes" rows="3" placeholder="13th salary, bonus, equity, benefits…"></textarea>
              </div>
            </div>
          </section>

          <section class="form-section">
            <h2>Submitted application</h2>
            <div class="form-grid">
              <div class="field">
                <label for="ae-cv">CV version</label>
                <input id="ae-cv" class="input" formControlName="cvVersion" placeholder="e.g. EU-2026-11" autocomplete="off" />
              </div>
              <div class="field span-2">
                <label for="ae-cover">Cover letter</label>
                <textarea id="ae-cover" class="textarea" formControlName="coverLetter" rows="4" placeholder="A reference (file name, link) or the letter itself"></textarea>
              </div>
            </div>
          </section>

          <section class="form-section">
            <h2><label for="ae-notes">Notes</label></h2>
            <textarea id="ae-notes" class="textarea" formControlName="notes" rows="8" placeholder="Markdown: interview prep, people you spoke to, next steps…"></textarea>
          </section>

          <div class="form-actions">
            @if (banner(); as message) {
              <span class="error-banner" role="alert"><app-icon name="circle-alert" />{{ message }}</span>
            }
            <a class="btn" [routerLink]="['/applications', a.id]">Cancel</a>
            <button type="submit" class="btn btn-primary" [disabled]="saving()">
              @if (saving()) {
                <app-icon name="loader" />
              }
              Save changes<kbd>{{ saveKeys }}</kbd>
            </button>
          </div>
        </form>
      } @else {
        <div class="skeleton-table" aria-busy="true"><span class="skeleton"></span><span class="skeleton"></span></div>
      }
    </div>
  `,
})
export class ApplicationEditPage implements OnInit, HasUnsavedChanges {
  readonly id = input.required({ transform: numberAttribute });
  protected readonly meta = inject(MetaService);
  private readonly api = inject(ApplicationsApi);
  private readonly settings = inject(SettingsStore);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  protected readonly application = signal<ApplicationDetail | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly banner = signal<string | null>(null);
  protected readonly today = todayIso();
  protected readonly saveKeys = isMac ? '⌘↵' : 'Ctrl ↵';

  protected readonly form = new FormGroup({
    priority: new FormControl<Priority>('Medium', { nonNullable: true }),
    submittedOn: new FormControl('', { nonNullable: true }),
    reasonForApplying: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(4000)] }),
    expectedSalary: salaryValueForm(null, this.settings.baseCurrency()),
    offeredSalary: salaryValueForm(null, this.settings.baseCurrency()),
    compensationNotes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(4000)] }),
    cvVersion: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    coverLetter: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(4000)] }),
    notes: new FormControl('', { nonNullable: true }),
  });

  private saved = false;

  constructor() {
    warnOnUnload(() => this.hasUnsavedChanges());
    inject(ShortcutsService).register([
      { keys: 'mod+Enter', description: 'Save application', group: 'Application form', run: () => void this.save(), allowInInputs: true },
    ]);
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saved;
  }

  async ngOnInit(): Promise<void> {
    try {
      const a = await this.api.get(this.id());
      this.form.patchValue({
        priority: a.priority,
        submittedOn: a.submittedOn ?? '',
        reasonForApplying: a.reasonForApplying ?? '',
        compensationNotes: a.compensationNotes ?? '',
        cvVersion: a.cvVersion ?? '',
        coverLetter: a.coverLetter ?? '',
        notes: a.notes ?? '',
      });
      this.form.setControl('expectedSalary', salaryValueForm(a.expectedSalary, this.settings.baseCurrency()));
      this.form.setControl('offeredSalary', salaryValueForm(a.offeredSalary, this.settings.baseCurrency()));
      this.application.set(a);
    } catch (error) {
      this.loadError.set(problemMessage(error, 'Could not load the application.'));
    }
  }

  protected err(path: string, label: string): string | null {
    return controlError(this.form.get(path), label);
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving() || !this.application()) {
      this.banner.set('Check the highlighted fields.');
      return;
    }
    this.saving.set(true);
    this.banner.set(null);
    const v = this.form.getRawValue();
    try {
      await this.api.update(this.id(), {
        priority: v.priority,
        submittedOn: v.submittedOn || null,
        reasonForApplying: clean(v.reasonForApplying),
        expectedSalary: toSalaryValue(this.form.controls.expectedSalary),
        offeredSalary: toSalaryValue(this.form.controls.offeredSalary),
        compensationNotes: clean(v.compensationNotes),
        cvVersion: clean(v.cvVersion),
        coverLetter: clean(v.coverLetter),
        notes: clean(v.notes),
      });
      this.saved = true;
      this.toasts.success('Application saved.');
      void this.router.navigate(['/applications', this.id()]);
    } catch (error) {
      this.banner.set(applyServerErrors(this.form, error).join(' ') || 'Check the highlighted fields.');
    } finally {
      this.saving.set(false);
    }
  }
}
