import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, computed, inject, input, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup } from '@angular/forms';
import { CompaniesApi } from '../core/api/companies.api';
import { controlError } from '../core/forms';
import { CompanyOption, NewCompanyInline } from '../core/models';
import { DuplicateWarning } from './duplicate-warning';
import { Icon } from './icon';

export type CompanyFieldForm = FormGroup<{
  companyId: FormControl<number | null>;
  newCompanyName: FormControl<string>;
}>;

export function companyFieldForm(companyId: number | null = null): CompanyFieldForm {
  return new FormGroup({
    companyId: new FormControl<number | null>(companyId),
    newCompanyName: new FormControl('', { nonNullable: true }),
  });
}

/** Request shape: an existing company id, or a company to create inline. */
export function companyFieldValue(form: CompanyFieldForm): { companyId: number | null; newCompany: NewCompanyInline | null } {
  const { companyId, newCompanyName } = form.getRawValue();
  const name = newCompanyName.trim();
  return companyId !== null ? { companyId, newCompany: null } : { companyId: null, newCompany: name ? { name } : null };
}

const MAX_SHOWN = 8;

/**
 * Searchable company picker (ARIA combobox). Type to filter existing companies; the last option
 * creates a new company with the typed name, written in the same request as the record that
 * needs it. Likely duplicates are flagged before saving.
 */
@Component({
  selector: 'app-company-field',
  imports: [Icon, DuplicateWarning],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="combo" [class.open]="open()">
      <app-icon [name]="isNew() ? 'plus' : 'building'" class="lead" [size]="14" />
      <input
        #field
        class="input"
        [class.invalid]="!!error()"
        [id]="inputId()"
        role="combobox"
        aria-autocomplete="list"
        [attr.aria-expanded]="open()"
        [attr.aria-controls]="inputId() + '-list'"
        [attr.aria-activedescendant]="open() && options().length ? inputId() + '-opt-' + active() : null"
        [attr.aria-invalid]="!!error()"
        [value]="text()"
        [placeholder]="loading() ? 'Loading companies…' : 'Search or create a company'"
        autocomplete="off"
        spellcheck="false"
        (input)="onInput($any($event.target).value)"
        (focus)="open.set(true)"
        (click)="open.set(true)"
        (blur)="onBlur()"
        (keydown)="onKeydown($event)"
      />
      @if (open() && options().length) {
        <ul class="menu listbox" role="listbox" [id]="inputId() + '-list'" aria-label="Companies">
          @for (option of options(); track option.key; let i = $index) {
            <li
              role="option"
              class="menu-item"
              [id]="inputId() + '-opt-' + i"
              [attr.aria-selected]="i === active()"
              [class.active]="i === active()"
              (mousedown)="$event.preventDefault()"
              (mousemove)="active.set(i)"
              (click)="choose(option)"
            >
              @if (option.create) {
                <app-icon name="plus" [size]="14" />
                <span>Create “{{ option.label }}”</span>
              } @else {
                <app-icon name="building" [size]="14" />
                <span class="truncate">{{ option.label }}</span>
                @if (option.archived) {
                  <span class="tag-flag end">Archived</span>
                }
              }
            </li>
          }
        </ul>
      }
    </div>
    @if (isNew()) {
      <span class="hint">A new company “{{ form().controls.newCompanyName.value }}” will be created. Add details later on its page.</span>
      <app-duplicate-warning [name]="form().controls.newCompanyName.value" />
    }
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
    .combo {
      position: relative;
    }
    .lead {
      position: absolute;
      left: 10px;
      top: 50%;
      transform: translateY(-50%);
      color: var(--text-subtle);
      pointer-events: none;
    }
    .input {
      padding-left: 30px;
    }
    .listbox {
      position: absolute;
      z-index: 20;
      top: calc(100% + 4px);
      left: 0;
      right: 0;
      margin: 0;
      list-style: none;
    }
    .menu-item.active {
      background: var(--surface-hover);
    }
    .hint {
      font-size: var(--text-xs);
      color: var(--text-subtle);
    }
    .error {
      font-size: var(--text-xs);
      color: var(--danger);
    }
  `,
})
export class CompanyField implements OnInit {
  private readonly api = inject(CompaniesApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = input.required<CompanyFieldForm>();
  readonly inputId = input('company');

  protected readonly companies = signal<CompanyOption[]>([]);
  protected readonly loading = signal(true);
  protected readonly text = signal('');
  protected readonly open = signal(false);
  protected readonly active = signal(0);
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  private readonly revision = signal(0);

  protected readonly isNew = computed(() => {
    this.revision();
    const f = this.form().getRawValue();
    return f.companyId === null && f.newCompanyName.trim() !== '';
  });

  protected readonly options = computed(() => {
    const query = this.text().trim().toLowerCase();
    const all = this.companies();
    const matches = (query ? all.filter((c) => c.name.toLowerCase().includes(query)) : all.filter((c) => !c.isArchived))
      .slice(0, MAX_SHOWN)
      .map((c) => ({ key: `c${c.id}`, id: c.id, label: c.name, archived: c.isArchived, create: false }));
    const exact = all.some((c) => c.name.toLowerCase() === query);
    return query && !exact ? [...matches, { key: 'new', id: null, label: this.text().trim(), archived: false, create: true }] : matches;
  });

  protected readonly error = computed(() => {
    this.revision();
    const f = this.form();
    return controlError(f.controls.companyId, 'Company') ?? controlError(f.controls.newCompanyName, 'Company name');
  });

  async ngOnInit(): Promise<void> {
    try {
      // Archived companies are included so an existing link still shows its name (D-30); they
      // are only offered when searched for.
      this.companies.set(await this.api.options(true));
      const { companyId, newCompanyName } = this.form().getRawValue();
      const selected = this.companies().find((c) => c.id === companyId);
      this.text.set(selected?.name ?? newCompanyName);
    } finally {
      this.loading.set(false);
    }
    this.form()
      .statusChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.revision.update((r) => r + 1));
  }

  /** Lets a parent focus the field (e.g. dialogs that open on it). */
  focus(): void {
    this.field().nativeElement.focus();
  }

  protected onInput(value: string): void {
    this.text.set(value);
    this.open.set(true);
    this.active.set(0);
    // Typing clears a previous choice until an option is picked.
    this.form().setValue({ companyId: null, newCompanyName: '' });
    this.form().markAsDirty();
    this.revision.update((r) => r + 1);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.options().length;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.open.set(true);
      this.active.update((i) => (count ? (i + 1) % count : 0));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.active.update((i) => (count ? (i - 1 + count) % count : 0));
    } else if (event.key === 'Enter' && this.open() && count) {
      event.preventDefault();
      this.choose(this.options()[this.active()]);
    } else if (event.key === 'Escape' && this.open()) {
      event.stopPropagation();
      this.open.set(false);
    }
  }

  protected onBlur(): void {
    this.open.set(false);
    this.form().markAllAsTouched();
    // Leaving the field with an unmatched name means "create it".
    const text = this.text().trim();
    if (this.form().controls.companyId.value === null && text) {
      const exact = this.companies().find((c) => c.name.toLowerCase() === text.toLowerCase());
      this.form().setValue(exact ? { companyId: exact.id, newCompanyName: '' } : { companyId: null, newCompanyName: text });
    }
    this.revision.update((r) => r + 1);
  }

  protected choose(option: { id: number | null; label: string; create: boolean }): void {
    this.form().setValue(option.create ? { companyId: null, newCompanyName: option.label } : { companyId: option.id, newCompanyName: '' });
    this.form().markAsDirty();
    this.text.set(option.label);
    this.open.set(false);
    this.revision.update((r) => r + 1);
  }
}
