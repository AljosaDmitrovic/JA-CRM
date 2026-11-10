import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

export interface Shortcut {
  /** 'mod+k', '?', 'c', 'g d' (sequence). 'mod' is ⌘ on macOS, Ctrl elsewhere. */
  keys: string;
  description: string;
  group: string;
  run: () => void;
  /** Allow while typing in an input (only for chorded shortcuts such as mod+k). */
  allowInInputs?: boolean;
}

const SEQUENCE_TIMEOUT_MS = 900;

export const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform || navigator.userAgent);

/** Renders 'mod+k' as '⌘ K' / 'Ctrl K' for display. */
export function displayKeys(keys: string): string[] {
  return keys
    .split(/[ +]/)
    .map((part) => (part === 'mod' ? (isMac ? '⌘' : 'Ctrl') : part.length === 1 ? part.toUpperCase() : part));
}

/**
 * One global keydown listener. Pages register their shortcuts while they are active; when two
 * registrations use the same keys, the most recent one wins (a page's "C" beats the global "C").
 */
@Injectable({ providedIn: 'root' })
export class ShortcutsService {
  private readonly registrations = signal<Shortcut[][]>([]);
  private pendingPrefix: string | null = null;
  private prefixTimer: ReturnType<typeof setTimeout> | undefined;
  private suspended = 0;

  /** Active shortcuts, most recent registration winning, for the help overlay. */
  readonly active = computed(() => {
    const byKeys = new Map<string, Shortcut>();
    for (const group of this.registrations()) {
      for (const shortcut of group) {
        byKeys.set(shortcut.keys, shortcut);
      }
    }
    return [...byKeys.values()];
  });

  constructor() {
    document.addEventListener('keydown', (event) => this.onKeydown(event));
  }

  /** Registers shortcuts for the lifetime of the calling component. */
  register(shortcuts: Shortcut[], destroyRef = inject(DestroyRef)): void {
    this.registrations.update((list) => [...list, shortcuts]);
    destroyRef.onDestroy(() => this.registrations.update((list) => list.filter((group) => group !== shortcuts)));
  }

  /** Dialogs suspend single-key shortcuts while open (Escape and focus stay with the dialog). */
  suspend(): () => void {
    this.suspended++;
    return () => this.suspended--;
  }

  private onKeydown(event: KeyboardEvent): void {
    if (event.defaultPrevented || event.isComposing) {
      return;
    }
    const key = this.describe(event);
    if (!key) {
      return;
    }
    const typing = isEditable(event.target);
    const candidates = this.active();

    const combined = this.pendingPrefix ? `${this.pendingPrefix} ${key}` : null;
    this.pendingPrefix = null;
    clearTimeout(this.prefixTimer);

    const match =
      (combined && candidates.find((s) => s.keys === combined)) || candidates.find((s) => s.keys === key);
    if (match) {
      const chorded = match.keys.startsWith('mod+');
      if ((typing && !match.allowInInputs) || (this.suspended > 0 && !chorded)) {
        return;
      }
      event.preventDefault();
      match.run();
      return;
    }

    if (!typing && this.suspended === 0 && candidates.some((s) => s.keys.startsWith(`${key} `))) {
      this.pendingPrefix = key;
      this.prefixTimer = setTimeout(() => (this.pendingPrefix = null), SEQUENCE_TIMEOUT_MS);
    }
  }

  private describe(event: KeyboardEvent): string | null {
    if (['Shift', 'Control', 'Alt', 'Meta'].includes(event.key)) {
      return null;
    }
    const mod = isMac ? event.metaKey : event.ctrlKey;
    if (event.altKey || (!mod && (event.metaKey || event.ctrlKey))) {
      return null;
    }
    const key = event.key.length === 1 ? event.key.toLowerCase() : event.key;
    return mod ? `mod+${key}` : key;
  }
}

function isEditable(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }
  return target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName);
}
