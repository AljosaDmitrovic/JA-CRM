import { AbstractControl, FormGroup, ValidationErrors } from '@angular/forms';
import { fieldErrors, problemMessage } from './problem';

/**
 * Puts a 400 validation problem onto the matching controls ("postedSalary.min" → that control) and
 * returns the messages that had no control, so the caller can show them in a banner.
 */
export function applyServerErrors(form: FormGroup, error: unknown, aliases: Record<string, string> = {}): string[] {
  const unmatched: string[] = [];
  const errors = fieldErrors(error);
  if (Object.keys(errors).length === 0) {
    return [problemMessage(error)];
  }
  for (const [key, message] of Object.entries(errors)) {
    const control = form.get(aliases[key] ?? key);
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: message });
      control.markAsTouched();
    } else {
      unmatched.push(message);
    }
  }
  return unmatched;
}

/** The message to show under a control: server message first, then the client rule that failed. */
export function controlError(control: AbstractControl | null, label = 'This field'): string | null {
  if (!control || !control.errors || !(control.touched || control.dirty)) {
    return null;
  }
  const e: ValidationErrors = control.errors;
  if (e['server']) return e['server'];
  if (e['required']) return `${label} is required.`;
  if (e['maxlength']) return `${label} must be at most ${e['maxlength'].requiredLength} characters.`;
  if (e['min']) return `${label} must be at least ${e['min'].min}.`;
  if (e['max']) return `${label} must be at most ${e['max'].max}.`;
  if (e['pattern'] || e['url']) return `${label} is not valid.`;
  return `${label} is not valid.`;
}

/** Trims, and turns empty strings into null (the API treats both the same). */
export function clean(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim();
  return trimmed === '' ? null : trimmed;
}

/** Client-side URL rule mirroring the server: http(s) URL or a bare domain (https:// is added). */
export function urlValidator(control: AbstractControl): ValidationErrors | null {
  const value = clean(control.value as string | null);
  if (!value) return null;
  try {
    const url = new URL(value.includes('://') ? value : `https://${value}`);
    return url.protocol === 'http:' || url.protocol === 'https:' ? null : { url: true };
  } catch {
    return { url: true };
  }
}
