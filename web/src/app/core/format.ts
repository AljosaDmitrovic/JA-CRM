import { DateFormatPreference, NormalizedSalary, PayPeriod, SalaryRange, SalaryValue } from './models';

const PERIOD_SUFFIX: Record<PayPeriod, string> = { Hourly: '/h', Monthly: '/mo', Annual: '/yr' };

const formatters = new Map<string, Intl.NumberFormat>();

function numberFormat(currency: string, digits: number): Intl.NumberFormat {
  const key = `${currency}|${digits}`;
  let formatter = formatters.get(key);
  if (!formatter) {
    try {
      formatter = new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency,
        currencyDisplay: 'code',
        minimumFractionDigits: 0,
        maximumFractionDigits: digits,
      });
    } catch {
      formatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: digits });
    }
    formatters.set(key, formatter);
  }
  return formatter;
}

/** "EUR 72,000" (no decimals for monthly/annual amounts, up to 2 for hourly). */
export function money(amount: number | null | undefined, currency: string, period: PayPeriod = 'Annual'): string {
  if (amount === null || amount === undefined) return '';
  return numberFormat(currency, period === 'Hourly' ? 2 : 0).format(amount);
}

/** "EUR 6,000 /mo · gross" */
export function salaryValueText(salary: SalaryValue | null | undefined): string {
  if (!salary) return '';
  return `${money(salary.amount, salary.currency, salary.period)} ${PERIOD_SUFFIX[salary.period]}${typeSuffix(salary.type)}`;
}

/** "EUR 70,000–90,000 /yr · gross", "from EUR 60,000 /yr", "up to CHF 8,000 /mo" */
export function salaryRangeText(range: SalaryRange | null | undefined): string {
  if (!range) return '';
  const { min, max, currency, period } = range;
  let amount: string;
  if (min !== null && max !== null) {
    amount = min === max ? money(min, currency, period) : `${money(min, currency, period)}–${money(max, currency, period).replace(currency, '').trim()}`;
  } else if (min !== null) {
    amount = `from ${money(min, currency, period)}`;
  } else {
    amount = `up to ${money(max, currency, period)}`;
  }
  return `${amount} ${PERIOD_SUFFIX[period]}${typeSuffix(range.type)}`;
}

/** Annual amount(s) in the base currency, e.g. "EUR 84,000/yr" or "EUR 72,000–84,000/yr". */
export function normalizedText(n: NormalizedSalary | null | undefined): string {
  if (!n || (n.annualMin === null && n.annualMax === null)) return '';
  const { annualMin: min, annualMax: max, baseCurrency } = n;
  if (min !== null && max !== null && min !== max) {
    return `${money(min, baseCurrency)}–${money(max, baseCurrency).replace(baseCurrency, '').trim()}/yr`;
  }
  return `${min === null ? 'up to ' : max === null ? 'from ' : ''}${money(min ?? max, baseCurrency)}/yr`;
}

export function comparabilityNote(n: NormalizedSalary | null | undefined): string | null {
  switch (n?.comparability) {
    case 'Net':
      return 'Net amount: not comparable with gross figures.';
    case 'UnknownType':
      return 'Gross or net unknown: not directly comparable.';
    case 'MissingRate':
      return 'No exchange rate for this currency (add one in Settings).';
    default:
      return null;
  }
}

function typeSuffix(type: string): string {
  return type === 'Gross' ? ' · gross' : type === 'Net' ? ' · net' : '';
}

// Dates ------------------------------------------------------------------------------------------

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

/** Date-only values ('yyyy-MM-dd') are formatted without any time-zone conversion. */
export function formatDateOnly(value: string | null | undefined, format: DateFormatPreference): string {
  if (!value) return '';
  const [y, m, d] = value.split('-');
  return format === 'Iso' ? `${y}-${m}-${d}` : `${d}.${m}.${y}`;
}

/** UTC timestamps are shown in local time. */
export function formatTimestamp(value: string | null | undefined, format: DateFormatPreference, withTime = false): string {
  if (!value) return '';
  const date = new Date(value);
  const day = format === 'Iso'
    ? `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
    : `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
  return withTime ? `${day} ${pad(date.getHours())}:${pad(date.getMinutes())}` : day;
}

const relative = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });

/** "just now", "5 minutes ago", "yesterday", "3 weeks ago". */
export function timeAgo(value: string | null | undefined, now = Date.now()): string {
  if (!value) return '';
  const seconds = Math.round((new Date(value).getTime() - now) / 1000);
  const abs = Math.abs(seconds);
  if (abs < 45) return 'just now';
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['minute', 60],
    ['hour', 3600],
    ['day', 86400],
    ['week', 604800],
    ['month', 2629800],
    ['year', 31557600],
  ];
  let unit: Intl.RelativeTimeFormatUnit = 'minute';
  let size = 60;
  for (const [u, s] of units) {
    if (abs >= s * 0.9) {
      unit = u;
      size = s;
    }
  }
  return relative.format(Math.round(seconds / size), unit);
}

/** Today's date as 'yyyy-MM-dd' in local time (for date inputs). */
export function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
