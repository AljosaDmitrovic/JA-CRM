import { signal } from '@angular/core';
import { problemMessage } from './problem';

/**
 * Loading / error / value state for one async source, as signals. Stale responses are ignored
 * when a newer load started in the meantime (fast filter changes, route changes).
 */
export class Loadable<T> {
  readonly value = signal<T | undefined>(undefined);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private sequence = 0;

  async load(source: () => Promise<T>): Promise<T | undefined> {
    const current = ++this.sequence;
    this.loading.set(true);
    this.error.set(null);
    try {
      const value = await source();
      if (current === this.sequence) {
        this.value.set(value);
      }
      return value;
    } catch (error) {
      if (current === this.sequence) {
        this.error.set(problemMessage(error, 'Could not load the data.'));
      }
      return undefined;
    } finally {
      if (current === this.sequence) {
        this.loading.set(false);
      }
    }
  }
}
