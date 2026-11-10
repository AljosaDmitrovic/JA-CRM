import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { controlError } from '../core/forms';
import { MetaService } from '../core/meta.service';
import { PayPeriod, SalaryRange, SalaryType, SalaryValue } from '../core/models';
import { SettingsStore } from '../core/settings.store';

export type SalaryValueForm = FormGroup<{
  amount: FormControl<number | null>;
  currency: FormControl<string>;
  period: FormControl<PayPeriod>;
  type: FormControl<SalaryType>;
}>;

export type SalaryRangeForm = FormGroup<{
  min: FormControl<number | null>;
  max: FormControl<number | null>;
  currency: FormControl<string>;
  period: FormControl<PayPeriod>;
  type: FormControl<SalaryType>;
}>;

const amountValidators = [Validators.min(0), Validators.max(100_000_000)];

function terms(currency: string, period: PayPeriod, type: SalaryType) {
  return {
    currency: new FormControl(currency, { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)] }),
    period: new FormControl<PayPeriod>(period, { nonNullable: true }),
    type: new FormControl<SalaryType>(type, { nonNullable: true }),
  };
}

export function salaryValueForm(value: SalaryValue | null, baseCurrency: string): SalaryValueForm {
  return new FormGroup({
    amount: new FormControl<number | null>(value?.amount ?? null, amountValidators),
    ...terms(value?.currency ?? baseCurrency, value?.period ?? 'Monthly', value?.type ?? 'Gross'),
  });
}

export function salaryRangeForm(value: SalaryRange | null, baseCurrency: string): SalaryRangeForm {
  return new FormGroup({
    min: new FormControl<number | null>(value?.min ?? null, amountValidators),
    max: new FormControl<number | null>(value?.max ?? null, amountValidators),
    ...terms(value?.currency ?? baseCurrency, value?.period ?? 'Annual', value?.type ?? 'Gross'),
  });
}

/** An empty amount means "no salary" (null), not an error. */
export function toSalaryValue(form: SalaryValueForm): SalaryValue | null {
  const v = form.getRawValue();
  return v.amount === null ? null : { amount: v.amount, currency: v.currency.trim().toUpperCase(), period: v.period, type: v.type };
}

export function toSalaryRange(form: SalaryRangeForm): SalaryRange | null {
  const v = form.getRawValue();
  return v.min === null && v.max === null
    ? null
    : { min: v.min, max: v.max, currency: v.currency.trim().toUpperCase(), period: v.period, type: v.type };
}

/** Amount (or min–max), currency, pay period and gross/net/unknown, on one row. */
@Component({
  selector: 'app-salary-fields',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="salary" [formGroup]="group()">
      <legend class="visually-hidden">{{ label() }}</legend>
      @if (range()) {
        <input class="input num" type="number" inputmode="decimal" min="0" step="any" formControlName="min" placeholder="Min" [attr.aria-label]="label() + ' minimum'" [attr.id]="idPrefix() + '-min'" />
        <span class="dash" aria-hidden="true">–</span>
        <input class="input num" type="number" inputmode="decimal" min="0" step="any" formControlName="max" placeholder="Max" [attr.aria-label]="label() + ' maximum'" />
      } @else {
        <input class="input num amount" type="number" inputmode="decimal" min="0" step="any" formControlName="amount" placeholder="Amount" [attr.aria-label]="label() + ' amount'" [attr.id]="idPrefix() + '-amount'" />
      }
      <input class="input currency" formControlName="currency" maxlength="3" [attr.list]="idPrefix() + '-currencies'" aria-label="Currency" autocomplete="off" spellcheck="false" />
      <datalist [id]="idPrefix() + '-currencies'">
        @for (code of currencies(); track code) {
          <option [value]="code"></option>
        }
      </datalist>
      <select class="select" formControlName="period" aria-label="Payment period">
        @for (option of meta.options('payPeriods'); track option.value) {
          <option [value]="option.value">{{ option.label }}</option>
        }
      </select>
      <select class="select" formControlName="type" aria-label="Gross or net">
        @for (option of meta.options('salaryTypes'); track option.value) {
          <option [value]="option.value">{{ option.label }}</option>
        }
      </select>
    </fieldset>
    @if (error(); as message) {
      <span class="error">{{ message }}</span>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .salary {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--s-2);
      margin: 0;
      padding: 0;
      border: 0;
      min-width: 0;
    }
    .num {
      flex: 1 1 110px;
      min-width: 90px;
    }
    .currency {
      width: 72px;
      text-transform: uppercase;
    }
    .select {
      width: auto;
      flex: 0 1 auto;
    }
    .dash {
      color: var(--text-subtle);
    }
    .error {
      font-size: var(--text-xs);
      color: var(--danger);
    }
  `,
})
export class SalaryFields {
  protected readonly meta = inject(MetaService);
  private readonly settings = inject(SettingsStore);

  readonly group = input.required<FormGroup>();
  readonly range = input(false);
  readonly label = input('Salary');
  readonly idPrefix = input('salary');

  protected currencies(): string[] {
    const rates = this.settings.settings()?.exchangeRates.map((r) => r.currency) ?? [];
    return [...new Set([this.settings.baseCurrency(), ...rates, ...this.meta.meta().commonCurrencies])];
  }

  protected error(): string | null {
    const g = this.group();
    for (const [name, label] of [
      ['amount', 'Amount'],
      ['min', 'Minimum'],
      ['max', 'Maximum'],
      ['currency', 'Currency'],
      ['period', 'Payment period'],
      ['type', 'Salary type'],
    ] as const) {
      const message = controlError(g.get(name), label);
      if (message) return message;
    }
    return null;
  }
}
