import { computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router } from '@angular/router';

/**
 * List state (search, filters, sort, page) lives in the URL query string, so reloads, the back
 * button and dashboard links ("/applications?stale=true") all work. Call in an injection context.
 */
export function queryState() {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const params = toSignal(route.queryParamMap, { requireSync: true });

  return {
    params,
    get: (key: string) => computed(() => params().get(key)),
    all: (key: string) => computed(() => params().getAll(key)),
    /** Merges into the current query; null/'' removes a key. Resets paging unless 'page' is set. */
    set(changes: Params): void {
      const next: Params = { page: null, ...changes };
      for (const [key, value] of Object.entries(next)) {
        if (value === '' || (Array.isArray(value) && value.length === 0)) next[key] = null;
      }
      void router.navigate([], { relativeTo: route, queryParams: next, queryParamsHandling: 'merge', replaceUrl: true });
    },
    clear(keep: string[] = []): void {
      const kept: Params = {};
      for (const key of keep) {
        const value = params().getAll(key);
        if (value.length) kept[key] = value.length === 1 ? value[0] : value;
      }
      void router.navigate([], { relativeTo: route, queryParams: kept, replaceUrl: true });
    },
  };
}
