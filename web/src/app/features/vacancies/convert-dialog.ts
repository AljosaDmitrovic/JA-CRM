import { DialogMeta } from '../../core/dialogs';
import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { VacanciesApi } from '../../core/api/vacancies.api';
import { MetaService } from '../../core/meta.service';
import { ApplicationStatus, Priority } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { Icon } from '../../shared/icon';
import { StatusSelect } from '../../shared/status-select';

export interface ConvertData {
  vacancyId: number;
  title: string;
  companyName: string;
}

/** "Convert to application": pick the starting status (default Interested) and priority. */
@Component({
  selector: 'app-convert-dialog',
  imports: [ReactiveFormsModule, Icon, StatusSelect],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="convert()">
      <header>
        <h2 id="convert-title">Convert to application</h2>
        <button type="button" class="btn btn-ghost btn-icon btn-sm" (click)="ref.close()" aria-label="Close"><app-icon name="x" /></button>
      </header>
      <div class="body stack">
        <p class="muted">
          <strong class="text">{{ data.title }}</strong> at {{ data.companyName }}. The posting is snapshotted now, so later edits to the
          vacancy do not change this application's history.
        </p>
        @if (error(); as message) {
          <div class="alert danger" role="alert"><app-icon name="circle-alert" />{{ message }}</div>
        }
        <div class="form-grid">
          <div class="field">
            <label for="convert-status">Initial status</label>
            <app-status-select selectId="convert-status" formControlName="status" />
          </div>
          <div class="field">
            <label for="convert-priority">Priority</label>
            <select id="convert-priority" class="select" formControlName="priority">
              @for (o of meta.options('priorities'); track o.value) {
                <option [ngValue]="o.value">{{ o.label }}</option>
              }
            </select>
          </div>
        </div>
      </div>
      <footer>
        <button type="button" class="btn" (click)="ref.close()">Cancel</button>
        <button type="submit" class="btn btn-primary" [disabled]="saving()" cdkFocusInitial>
          @if (saving()) {
            <app-icon name="loader" />
          }
          Create application
        </button>
      </footer>
    </form>
  `,
})
export class ConvertDialog {
  static readonly dialog: DialogMeta = { labelledBy: 'convert-title' };

  protected readonly ref = inject<DialogRef<number>>(DialogRef);
  protected readonly data = inject<ConvertData>(DIALOG_DATA);
  protected readonly meta = inject(MetaService);
  private readonly api = inject(VacanciesApi);
  private readonly toasts = inject(ToastService);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = new FormGroup({
    status: new FormControl<ApplicationStatus>('Interested', { nonNullable: true }),
    priority: new FormControl<Priority>('Medium', { nonNullable: true }),
  });

  protected async convert(): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      const { status, priority } = this.form.getRawValue();
      const created = await this.api.convert(this.data.vacancyId, status, priority);
      this.toasts.success('Application created.');
      this.ref.close(created.id);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
