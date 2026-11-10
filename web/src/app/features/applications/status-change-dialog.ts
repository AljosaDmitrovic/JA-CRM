import { DialogMeta } from '../../core/dialogs';
import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApplicationsApi } from '../../core/api/applications.api';
import { MetaService } from '../../core/meta.service';
import { ApplicationListItem, ApplicationStatus } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { guardDialogClose } from '../../core/unsaved-changes';
import { Icon } from '../../shared/icon';
import { StatusSelect } from '../../shared/status-select';

export interface StatusChangeData {
  applicationId: number;
  current: ApplicationStatus;
  /** Preselected target (e.g. from the "Move to…" menu); defaults to the current status. */
  target?: ApplicationStatus;
}

/** Change the status with an optional note, recorded in the status history. */
@Component({
  selector: 'app-status-change-dialog',
  imports: [ReactiveFormsModule, Icon, StatusSelect],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()">
      <header>
        <h2 id="status-title">Change status</h2>
        <button type="button" class="btn btn-ghost btn-icon btn-sm" (click)="ref.close()" aria-label="Close"><app-icon name="x" /></button>
      </header>
      <div class="body">
        @if (error(); as message) {
          <div class="alert danger" role="alert"><app-icon name="circle-alert" />{{ message }}</div>
        }
        <div class="form-grid">
          <div class="field span-2">
            <label for="sc-status">New status <span class="subtle">(currently {{ meta.statusLabel(data.current) }})</span></label>
            <app-status-select selectId="sc-status" formControlName="status" />
          </div>
          <div class="field span-2">
            <label for="sc-note">Note <span class="subtle">(optional, kept in the history)</span></label>
            <textarea id="sc-note" class="textarea" formControlName="note" rows="3" placeholder="e.g. Call with the recruiter went well; tech interview next week" cdkFocusInitial></textarea>
          </div>
        </div>
      </div>
      <footer>
        <button type="button" class="btn" (click)="ref.close()">Cancel</button>
        <button type="submit" class="btn btn-primary" [disabled]="saving() || form.controls.status.value === data.current">
          @if (saving()) {
            <app-icon name="loader" />
          }
          Change status
        </button>
      </footer>
    </form>
  `,
})
export class StatusChangeDialog {
  static readonly dialog: DialogMeta = { labelledBy: 'status-title' };

  protected readonly ref = inject<DialogRef<ApplicationListItem>>(DialogRef);
  protected readonly data = inject<StatusChangeData>(DIALOG_DATA);
  protected readonly meta = inject(MetaService);
  private readonly api = inject(ApplicationsApi);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = new FormGroup({
    status: new FormControl<ApplicationStatus>(this.data.target ?? this.data.current, { nonNullable: true }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
  });

  constructor() {
    guardDialogClose(() => this.form.dirty && !this.saving());
  }

  protected async save(): Promise<void> {
    const { status, note } = this.form.getRawValue();
    if (this.saving() || status === this.data.current) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      this.ref.close(await this.api.changeStatus(this.data.applicationId, status, note.trim() || null));
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
