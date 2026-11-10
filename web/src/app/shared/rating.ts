import { ChangeDetectionStrategy, Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Icon } from './icon';

/** Read-only 1–5 rating. */
@Component({
  selector: 'app-rating',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (value(); as v) {
      <span class="stars" [attr.aria-label]="v + ' out of 5'" role="img">
        @for (n of [1, 2, 3, 4, 5]; track n) {
          <app-icon name="star" [size]="12" [class.on]="n <= v" />
        }
      </span>
    } @else {
      <span class="subtle">—</span>
    }
  `,
  styles: `
    .stars {
      display: inline-flex;
      gap: 1px;
      color: var(--border-strong);
    }
    .on {
      color: var(--text-muted);
      fill: currentColor;
    }
    .on ::ng-deep svg {
      fill: currentColor;
    }
  `,
})
export class Rating {
  readonly value = input<number | null>(null);
}

/** 1–5 rating input as a radio group; choosing the current value again clears it. */
@Component({
  selector: 'app-rating-input',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => RatingInput), multi: true }],
  template: `
    <div class="stars" role="radiogroup" [attr.aria-label]="label()">
      @for (n of [1, 2, 3, 4, 5]; track n) {
        <button
          type="button"
          role="radio"
          [attr.aria-checked]="value() === n"
          [attr.aria-label]="n + (n === 1 ? ' star' : ' stars')"
          [class.on]="(hover() ?? value() ?? 0) >= n"
          [tabindex]="(value() ?? 1) === n ? 0 : -1"
          (click)="set(n)"
          (mouseenter)="hover.set(n)"
          (mouseleave)="hover.set(null)"
          (keydown)="onKey($event, n)"
          [disabled]="disabled()"
        >
          <app-icon name="star" [size]="16" />
        </button>
      }
      @if (value()) {
        <button type="button" class="btn btn-ghost btn-sm clear" (click)="set(null)" [disabled]="disabled()">Clear</button>
      }
    </div>
  `,
  styles: `
    .stars {
      display: flex;
      align-items: center;
      gap: 2px;
      min-height: var(--control-height);
    }
    button[role='radio'] {
      display: grid;
      place-items: center;
      width: 26px;
      height: 26px;
      padding: 0;
      border: 0;
      border-radius: var(--radius-sm);
      background: transparent;
      color: var(--border-strong);
      cursor: pointer;
    }
    button.on {
      color: var(--warning);
    }
    button.on ::ng-deep svg {
      fill: currentColor;
    }
    .clear {
      margin-left: var(--s-2);
    }
  `,
})
export class RatingInput implements ControlValueAccessor {
  readonly label = input('Rating');
  protected readonly value = signal<number | null>(null);
  protected readonly hover = signal<number | null>(null);
  protected readonly disabled = signal(false);
  private onChange: (value: number | null) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: number | null): void {
    this.value.set(value);
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  protected set(value: number | null): void {
    const next = value === this.value() ? null : value;
    this.value.set(next);
    this.onChange(next);
    this.onTouched();
  }

  protected onKey(event: KeyboardEvent, n: number): void {
    const step = event.key === 'ArrowRight' || event.key === 'ArrowUp' ? 1 : event.key === 'ArrowLeft' || event.key === 'ArrowDown' ? -1 : 0;
    if (step) {
      event.preventDefault();
      const next = Math.min(5, Math.max(1, n + step));
      this.value.set(next);
      this.onChange(next);
      ((event.currentTarget as HTMLElement).parentElement?.children[next - 1] as HTMLElement | undefined)?.focus();
    }
  }
}
