import { ChangeDetectionStrategy, Component, ElementRef, computed, forwardRef, input, signal, viewChild } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Icon } from './icon';

/** Technology tags: type and press Enter or comma; Backspace on an empty field removes the last
 * tag. Existing tags are suggested (case-insensitive duplicates are ignored). */
@Component({
  selector: 'app-tag-input',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => TagInput), multi: true }],
  template: `
    <div class="tags" [class.disabled]="disabled()" (click)="field.focus()">
      @for (tag of tags(); track tag) {
        <span class="chip strong">
          {{ tag }}
          <button type="button" class="remove" (click)="remove(tag); $event.stopPropagation()" [attr.aria-label]="'Remove ' + tag" [disabled]="disabled()">
            <app-icon name="x" [size]="12" />
          </button>
        </span>
      }
      <input
        #field
        [id]="inputId()"
        [attr.list]="inputId() + '-suggestions'"
        [placeholder]="tags().length ? '' : placeholder()"
        [disabled]="disabled()"
        [value]="draft()"
        (input)="onInput($any($event.target).value)"
        (keydown)="onKeydown($event)"
        (blur)="commit(); touched()"
        autocomplete="off"
        spellcheck="false"
      />
      <datalist [id]="inputId() + '-suggestions'">
        @for (s of available(); track s) {
          <option [value]="s"></option>
        }
      </datalist>
    </div>
  `,
  styles: `
    .tags {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px;
      min-height: var(--control-height);
      padding: 3px 6px;
      border: 1px solid var(--border-strong);
      border-radius: var(--radius);
      background: var(--surface);
      cursor: text;

      &:focus-within {
        border-color: var(--accent);
        box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent) 18%, transparent);
      }

      &.disabled {
        background: var(--surface-sunken);
      }
    }
    input {
      flex: 1 1 120px;
      min-width: 80px;
      height: 24px;
      padding: 0 4px;
      border: 0;
      outline: none;
      background: transparent;
      box-shadow: none;
    }
    .remove {
      display: inline-flex;
      margin-right: -4px;
      padding: 1px;
      border: 0;
      border-radius: 3px;
      background: transparent;
      color: var(--text-subtle);
      cursor: pointer;

      &:hover {
        color: var(--text);
        background: var(--surface-active);
      }
    }
  `,
})
export class TagInput implements ControlValueAccessor {
  readonly inputId = input.required<string>();
  readonly suggestions = input<string[]>([]);
  readonly placeholder = input('Add…');

  protected readonly tags = signal<string[]>([]);
  protected readonly draft = signal('');
  protected readonly disabled = signal(false);
  protected readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  protected readonly available = computed(() => {
    const taken = new Set(this.tags().map((t) => t.toLowerCase()));
    return this.suggestions().filter((s) => !taken.has(s.toLowerCase()));
  });

  private onChange: (value: string[]) => void = () => {};
  protected touched: () => void = () => {};

  writeValue(value: string[] | null): void {
    this.tags.set([...(value ?? [])]);
  }

  registerOnChange(fn: (value: string[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.touched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  protected onInput(value: string): void {
    // A comma (or a pasted list) separates tags.
    if (value.includes(',')) {
      const parts = value.split(',');
      this.draft.set(parts.pop() ?? '');
      parts.forEach((p) => this.add(p));
      this.field().nativeElement.value = this.draft();
    } else {
      this.draft.set(value);
      // Picking a datalist suggestion fires input with the full value; add it right away.
      if (this.available().some((s) => s === value)) {
        this.commit();
      }
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.commit();
    } else if (event.key === 'Backspace' && this.draft() === '' && this.tags().length) {
      this.remove(this.tags()[this.tags().length - 1]);
    }
  }

  protected commit(): void {
    this.add(this.draft());
    this.draft.set('');
    this.field().nativeElement.value = '';
  }

  protected remove(tag: string): void {
    this.tags.update((tags) => tags.filter((t) => t !== tag));
    this.onChange(this.tags());
  }

  private add(raw: string): void {
    const tag = raw.trim().replace(/\s+/g, ' ');
    if (!tag || this.tags().some((t) => t.toLowerCase() === tag.toLowerCase())) {
      return;
    }
    // Reuse the existing spelling of a known tag ("postgresql" → "PostgreSQL").
    const known = this.suggestions().find((s) => s.toLowerCase() === tag.toLowerCase());
    this.tags.update((tags) => [...tags, known ?? tag]);
    this.onChange(this.tags());
  }
}
