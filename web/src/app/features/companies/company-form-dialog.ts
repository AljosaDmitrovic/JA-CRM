import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CompaniesApi } from '../../core/api/companies.api';
import { LookupsApi } from '../../core/api/misc.api';
import { applyServerErrors, clean, controlError, urlValidator } from '../../core/forms';
import { MetaService } from '../../core/meta.service';
import { CompanyDetail, CompanySize, Lookups } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { guardDialogClose } from '../../core/unsaved-changes';
import { DuplicateWarning } from '../../shared/duplicate-warning';
import { Icon } from '../../shared/icon';
import { RatingInput } from '../../shared/rating';

export interface CompanyFormData {
  company?: CompanyDetail;
}

/** Add or edit a company. Only the name is required, so a company can be captured in seconds. */
@Component({
  selector: 'app-company-form-dialog',
  imports: [ReactiveFormsModule, Icon, DuplicateWarning, RatingInput],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" aria-labelledby="company-form-title">
      <header>
        <h2 id="company-form-title">{{ editing ? 'Edit company' : 'Add company' }}</h2>
        <button type="button" class="btn btn-ghost btn-icon btn-sm" (click)="ref.close()" aria-label="Close">
          <app-icon name="x" />
        </button>
      </header>
      <div class="body">
        @if (banner(); as message) {
          <div class="alert danger" role="alert"><app-icon name="circle-alert" />{{ message }}</div>
        }
        <div class="form-grid">
          <div class="field span-2">
            <label for="company-name" class="required">Name</label>
            <input id="company-name" class="input" formControlName="name" autocomplete="off" cdkFocusInitial />
            @if (err('name', 'Name'); as e) {
              <span class="error">{{ e }}</span>
            }
          </div>
          <div class="field">
            <label for="company-website">Website</label>
            <input id="company-website" class="input" formControlName="website" placeholder="acme.com" autocomplete="off" />
            @if (err('website', 'Website'); as e) {
              <span class="error">{{ e }}</span>
            }
          </div>
          <div class="field">
            <label for="company-careers">Careers page</label>
            <input id="company-careers" class="input" formControlName="careersUrl" placeholder="acme.com/careers" autocomplete="off" />
            @if (err('careersUrl', 'Careers page'); as e) {
              <span class="error">{{ e }}</span>
            }
          </div>
          <div class="span-2">
            <app-duplicate-warning [name]="name()" [website]="website()" [excludeId]="data?.company?.id ?? null" />
          </div>
          <div class="field">
            <label for="company-industry">Industry</label>
            <input id="company-industry" class="input" formControlName="industry" list="company-industries" autocomplete="off" />
            <datalist id="company-industries">
              @for (v of lookups()?.industries ?? []; track v) {
                <option [value]="v"></option>
              }
            </datalist>
          </div>
          <div class="field">
            <label for="company-size">Company size</label>
            <select id="company-size" class="select" formControlName="size">
              <option [ngValue]="null">—</option>
              @for (option of meta.options('companySizes'); track option.value) {
                <option [ngValue]="option.value">{{ option.label }} employees</option>
              }
            </select>
          </div>
          <div class="field">
            <label for="company-country">Country</label>
            <input id="company-country" class="input" formControlName="country" list="company-countries" autocomplete="off" />
            <datalist id="company-countries">
              @for (v of lookups()?.countries ?? []; track v) {
                <option [value]="v"></option>
              }
            </datalist>
          </div>
          <div class="field">
            <label for="company-city">City</label>
            <input id="company-city" class="input" formControlName="city" list="company-cities" autocomplete="off" />
            <datalist id="company-cities">
              @for (v of lookups()?.cities ?? []; track v) {
                <option [value]="v"></option>
              }
            </datalist>
          </div>
          <div class="field">
            <label for="company-hq">Headquarters</label>
            <input id="company-hq" class="input" formControlName="headquarters" autocomplete="off" />
          </div>
          <div class="field">
            <span class="label" id="company-rating-label">Personal rating</span>
            <app-rating-input formControlName="rating" label="Personal rating" />
          </div>
          <div class="field span-2">
            <label for="company-notes">Notes <span class="subtle">(Markdown)</span></label>
            <textarea id="company-notes" class="textarea" formControlName="notes" rows="5"></textarea>
          </div>
        </div>
      </div>
      <footer>
        <span class="left">Only the name is required.</span>
        <button type="button" class="btn" (click)="ref.close()">Cancel</button>
        <button type="submit" class="btn btn-primary" [disabled]="saving()">
          @if (saving()) {
            <app-icon name="loader" />
          }
          {{ editing ? 'Save' : 'Add company' }}
        </button>
      </footer>
    </form>
  `,
})
export class CompanyFormDialog implements OnInit {
  protected readonly ref = inject<DialogRef<CompanyDetail>>(DialogRef);
  protected readonly data = inject<CompanyFormData | null>(DIALOG_DATA, { optional: true });
  protected readonly meta = inject(MetaService);
  private readonly api = inject(CompaniesApi);
  private readonly lookupsApi = inject(LookupsApi);
  private readonly toasts = inject(ToastService);

  protected readonly editing = !!this.data?.company;
  protected readonly saving = signal(false);
  protected readonly banner = signal<string | null>(null);
  protected readonly lookups = signal<Lookups | null>(null);

  protected readonly form = new FormGroup({
    name: new FormControl(this.data?.company?.name ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    website: new FormControl(this.data?.company?.website ?? '', { nonNullable: true, validators: [urlValidator] }),
    careersUrl: new FormControl(this.data?.company?.careersUrl ?? '', { nonNullable: true, validators: [urlValidator] }),
    industry: new FormControl(this.data?.company?.industry ?? '', { nonNullable: true }),
    headquarters: new FormControl(this.data?.company?.headquarters ?? '', { nonNullable: true }),
    country: new FormControl(this.data?.company?.country ?? '', { nonNullable: true }),
    city: new FormControl(this.data?.company?.city ?? '', { nonNullable: true }),
    size: new FormControl<CompanySize | null>(this.data?.company?.size ?? null),
    rating: new FormControl<number | null>(this.data?.company?.rating ?? null),
    notes: new FormControl(this.data?.company?.notes ?? '', { nonNullable: true }),
  });

  constructor() {
    guardDialogClose(() => this.form.dirty && !this.saving());
  }

  protected readonly name = toSignal(this.form.controls.name.valueChanges, { initialValue: this.form.controls.name.value });
  protected readonly website = toSignal(this.form.controls.website.valueChanges, { initialValue: this.form.controls.website.value });

  async ngOnInit(): Promise<void> {
    this.lookups.set(await this.lookupsApi.get().catch(() => null));
  }

  protected err(name: keyof typeof this.form.controls, label: string): string | null {
    return controlError(this.form.controls[name], label);
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.banner.set(null);
    const v = this.form.getRawValue();
    const request = {
      name: v.name.trim(),
      website: clean(v.website),
      careersUrl: clean(v.careersUrl),
      industry: clean(v.industry),
      headquarters: clean(v.headquarters),
      country: clean(v.country),
      city: clean(v.city),
      size: v.size,
      rating: v.rating,
      notes: clean(v.notes),
    };
    try {
      const company = this.editing ? await this.api.update(this.data!.company!.id, request) : await this.api.create(request);
      this.toasts.success(this.editing ? 'Company saved.' : `${company.name} added.`);
      this.ref.close(company);
    } catch (error) {
      this.banner.set(applyServerErrors(this.form, error).join(' ') || null);
    } finally {
      this.saving.set(false);
    }
  }
}
