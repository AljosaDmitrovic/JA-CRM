import { ChangeDetectionStrategy, Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CsvEntity, DataApi } from '../../core/api/misc.api';
import { applyServerErrors, controlError } from '../../core/forms';
import { MetaService } from '../../core/meta.service';
import { DateFormatPreference, ExchangeRate, Settings, ThemePreference } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { SettingsStore } from '../../core/settings.store';
import { ToastService } from '../../core/toast.service';
import { Confirm } from '../../shared/confirm-dialog';
import { Icon, IconName } from '../../shared/icon';
import { AppDatePipe } from '../../shared/pipes';

type RateForm = FormGroup<{ currency: FormControl<string>; rateToBase: FormControl<number | null>; updatedOn: FormControl<string> }>;

@Component({
  selector: 'app-settings-page',
  imports: [ReactiveFormsModule, Icon, AppDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
})
export class SettingsPage {
  protected readonly meta = inject(MetaService);
  protected readonly store = inject(SettingsStore);
  protected readonly data = inject(DataApi);
  private readonly toasts = inject(ToastService);
  private readonly confirm = inject(Confirm);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('file');

  protected readonly themeIcons: Record<ThemePreference, IconName> = { System: 'monitor', Light: 'sun', Dark: 'moon' };
  protected readonly csvEntities: { entity: CsvEntity; label: string }[] = [
    { entity: 'companies', label: 'Companies' },
    { entity: 'vacancies', label: 'Vacancies' },
    { entity: 'applications', label: 'Applications' },
    { entity: 'status-history', label: 'Status history' },
  ];

  protected readonly savingGeneral = signal(false);
  protected readonly savingRates = signal(false);
  protected readonly importing = signal(false);
  protected readonly generalError = signal<string | null>(null);
  protected readonly ratesError = signal<string | null>(null);

  protected readonly general = new FormGroup({
    baseCurrency: new FormControl('EUR', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)] }),
    workingHoursPerMonth: new FormControl(168, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(744)] }),
    staleThresholdDays: new FormControl(14, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(365)] }),
    dateFormat: new FormControl<DateFormatPreference>('DayMonthYear', { nonNullable: true }),
  });

  protected readonly rates = new FormGroup({ rates: new FormArray<RateForm>([]) });

  constructor() {
    const settings = this.store.settings();
    if (settings) this.reset(settings);
  }

  protected err(form: FormGroup, path: string, label: string): string | null {
    return controlError(form.get(path), label);
  }

  /** The theme applies and saves at once; everything else uses the Save button. */
  protected async setTheme(theme: ThemePreference): Promise<void> {
    const current = this.store.settings();
    if (!current || current.theme === theme) return;
    try {
      await this.store.update({ ...this.withoutRates(current), theme });
    } catch (error) {
      this.toasts.error(problemMessage(error));
    }
  }

  protected async saveGeneral(): Promise<void> {
    this.general.markAllAsTouched();
    const current = this.store.settings();
    if (this.general.invalid || !current || this.savingGeneral()) return;
    this.savingGeneral.set(true);
    this.generalError.set(null);
    const v = this.general.getRawValue();
    try {
      const saved = await this.store.update({ ...v, theme: current.theme, baseCurrency: v.baseCurrency.toUpperCase() });
      this.reset(saved);
      this.toasts.success(saved.baseCurrency !== current.baseCurrency ? `Base currency is now ${saved.baseCurrency}; rates were converted.` : 'Settings saved.');
    } catch (error) {
      this.generalError.set(applyServerErrors(this.general, error).join(' ') || null);
    } finally {
      this.savingGeneral.set(false);
    }
  }

  protected addRate(): void {
    this.rates.controls.rates.push(this.rateForm({ currency: '', rateToBase: 0, updatedOn: '' }));
    this.rates.markAsDirty();
    setTimeout(() => (document.querySelector('.rates tbody tr:last-child input') as HTMLInputElement | null)?.focus());
  }

  protected removeRate(index: number): void {
    this.rates.controls.rates.removeAt(index);
    this.rates.markAsDirty();
  }

  protected async saveRates(): Promise<void> {
    this.rates.markAllAsTouched();
    if (this.rates.invalid || this.savingRates()) return;
    this.savingRates.set(true);
    this.ratesError.set(null);
    const rates = this.rates.controls.rates.getRawValue().map((r) => ({
      currency: r.currency.trim().toUpperCase(),
      // No date: unchanged rates keep theirs, new or changed ones get today's (server side).
      rateToBase: r.rateToBase ?? 0,
    }));
    try {
      const saved = await this.store.saveRates(rates);
      this.reset(saved);
      this.toasts.success('Exchange rates saved.');
    } catch (error) {
      this.ratesError.set(applyServerErrors(this.rates, error).join(' ') || null);
    } finally {
      this.savingRates.set(false);
    }
  }

  protected chooseFile(): void {
    this.fileInput()?.nativeElement.click();
  }

  protected async importFile(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const ok = await this.confirm.ask({
      title: 'Replace all data?',
      message: `Importing “${file.name}” deletes every company, vacancy, application, status history entry, exchange rate and setting, and replaces them with the file's contents. A backup of the current database is written first.`,
      confirmLabel: 'Replace everything',
      danger: true,
    });
    if (!ok) return;
    this.importing.set(true);
    try {
      const summary = await this.data.import(file);
      this.store.invalidate();
      await this.store.load();
      this.reset(this.store.settings()!);
      this.toasts.success(
        `Imported ${summary.companies} companies, ${summary.vacancies} vacancies and ${summary.applications} applications. Previous data backed up as ${summary.backupFile}.`,
      );
    } catch (error) {
      this.toasts.error(problemMessage(error, 'The import failed. Nothing was changed.'));
    } finally {
      this.importing.set(false);
    }
  }

  private reset(settings: Settings): void {
    this.general.reset({
      baseCurrency: settings.baseCurrency,
      workingHoursPerMonth: settings.workingHoursPerMonth,
      staleThresholdDays: settings.staleThresholdDays,
      dateFormat: settings.dateFormat,
    });
    const array = this.rates.controls.rates;
    array.clear();
    settings.exchangeRates.forEach((r) => array.push(this.rateForm(r)));
    this.rates.markAsPristine();
  }

  private rateForm(rate: ExchangeRate): RateForm {
    return new FormGroup({
      currency: new FormControl(rate.currency, { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)] }),
      rateToBase: new FormControl<number | null>(rate.rateToBase || null, [Validators.required, Validators.min(0.000001)]),
      updatedOn: new FormControl(rate.updatedOn, { nonNullable: true }),
    });
  }

  private withoutRates(settings: Settings) {
    const { exchangeRates: _, ...rest } = settings;
    return rest;
  }
}
