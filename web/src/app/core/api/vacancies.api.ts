import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApplicationStatus,
  CreatedId,
  Paged,
  Priority,
  QueryParams,
  VacancyDetail,
  VacancyListItem,
  VacancyRequest,
} from '../models';
import { toParams } from './http-params';

@Injectable({ providedIn: 'root' })
export class VacanciesApi {
  private readonly http = inject(HttpClient);

  list(query: QueryParams): Promise<Paged<VacancyListItem>> {
    return firstValueFrom(this.http.get<Paged<VacancyListItem>>('/api/vacancies', { params: toParams(query) }));
  }

  get(id: number): Promise<VacancyDetail> {
    return firstValueFrom(this.http.get<VacancyDetail>(`/api/vacancies/${id}`));
  }

  create(request: VacancyRequest): Promise<VacancyDetail> {
    return firstValueFrom(this.http.post<VacancyDetail>('/api/vacancies', request));
  }

  update(id: number, request: VacancyRequest): Promise<VacancyDetail> {
    return firstValueFrom(this.http.put<VacancyDetail>(`/api/vacancies/${id}`, request));
  }

  setArchived(id: number, archived: boolean): Promise<unknown> {
    return firstValueFrom(this.http.post(`/api/vacancies/${id}/${archived ? 'archive' : 'unarchive'}`, {}));
  }

  delete(id: number): Promise<unknown> {
    return firstValueFrom(this.http.delete(`/api/vacancies/${id}`));
  }

  convert(id: number, status: ApplicationStatus, priority: Priority): Promise<CreatedId> {
    return firstValueFrom(this.http.post<CreatedId>(`/api/vacancies/${id}/convert`, { status, priority }));
  }
}
