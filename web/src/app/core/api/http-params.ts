import { HttpParams } from '@angular/common/http';
import { QueryParams } from '../models';

/** Builds HttpParams, skipping empty values and repeating array keys (?tech=a&tech=b). */
export function toParams(query: QueryParams): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === null || value === undefined || value === '') {
      continue;
    }
    if (Array.isArray(value)) {
      for (const item of value) {
        params = params.append(key, item);
      }
    } else {
      params = params.set(key, String(value));
    }
  }
  return params;
}
