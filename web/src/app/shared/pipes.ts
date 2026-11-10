import { Pipe, PipeTransform, inject } from '@angular/core';
import { SettingsStore } from '../core/settings.store';
import { formatDateOnly, formatTimestamp, timeAgo } from '../core/format';

// Impure on purpose: they read the date-format setting (a signal), so they must re-run when it
// changes. Formatting is cheap.

/** Date-only value ('yyyy-MM-dd') in the configured format. */
@Pipe({ name: 'appDate', pure: false })
export class AppDatePipe implements PipeTransform {
  private readonly settings = inject(SettingsStore);

  transform(value: string | null | undefined): string {
    return formatDateOnly(value, this.settings.dateFormat());
  }
}

/** UTC timestamp in local time and the configured format; 'time' adds hours and minutes. */
@Pipe({ name: 'appTimestamp', pure: false })
export class AppTimestampPipe implements PipeTransform {
  private readonly settings = inject(SettingsStore);

  transform(value: string | null | undefined, withTime: 'time' | '' = ''): string {
    return formatTimestamp(value, this.settings.dateFormat(), withTime === 'time');
  }
}

@Pipe({ name: 'timeAgo' })
export class TimeAgoPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return timeAgo(value);
  }
}
