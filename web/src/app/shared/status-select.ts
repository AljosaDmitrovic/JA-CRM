import { ChangeDetectionStrategy, Component, forwardRef, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MetaService } from '../core/meta.service';
import { ApplicationStatus } from '../core/models';

/** Native select of statuses grouped by phase (optgroups): accessible and keyboard-friendly. */
@Component({
  selector: 'app-status-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => StatusSelect), multi: true }],
  template: `
    <select class="select" [id]="selectId()" [disabled]="disabled()" (change)="choose($any($event.target).value)" (blur)="onTouched()">
      @for (phase of meta.meta().phases; track phase.value) {
        <optgroup [label]="phase.label">
          @for (status of phase.statuses; track status) {
            <option [value]="status" [selected]="status === value()">{{ meta.statusLabel(status) }}</option>
          }
        </optgroup>
      }
    </select>
  `,
})
export class StatusSelect implements ControlValueAccessor {
  protected readonly meta = inject(MetaService);
  readonly selectId = input('status');
  protected readonly value = signal<ApplicationStatus>('Interested');
  protected readonly disabled = signal(false);
  private onChange: (value: ApplicationStatus) => void = () => {};
  protected onTouched: () => void = () => {};

  writeValue(value: ApplicationStatus | null): void {
    this.value.set(value ?? 'Interested');
  }

  registerOnChange(fn: (value: ApplicationStatus) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  protected choose(value: ApplicationStatus): void {
    this.value.set(value);
    this.onChange(value);
  }
}
