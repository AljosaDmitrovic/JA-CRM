import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApplicationDetail,
  ApplicationListItem,
  ApplicationStatus,
  ApplicationUpdateRequest,
  CreateApplicationRequest,
  CreatedId,
  Paged,
  QueryParams,
} from '../models';
import { toParams } from './http-params';

@Injectable({ providedIn: 'root' })
export class ApplicationsApi {
  private readonly http = inject(HttpClient);

  list(query: QueryParams): Promise<Paged<ApplicationListItem>> {
    return firstValueFrom(this.http.get<Paged<ApplicationListItem>>('/api/applications', { params: toParams(query) }));
  }

  board(query: QueryParams): Promise<ApplicationListItem[]> {
    return firstValueFrom(this.http.get<ApplicationListItem[]>('/api/applications/board', { params: toParams(query) }));
  }

  get(id: number): Promise<ApplicationDetail> {
    return firstValueFrom(this.http.get<ApplicationDetail>(`/api/applications/${id}`));
  }

  create(request: CreateApplicationRequest): Promise<CreatedId> {
    return firstValueFrom(this.http.post<CreatedId>('/api/applications', request));
  }

  update(id: number, request: ApplicationUpdateRequest): Promise<ApplicationDetail> {
    return firstValueFrom(this.http.put<ApplicationDetail>(`/api/applications/${id}`, request));
  }

  changeStatus(id: number, status: ApplicationStatus, note: string | null = null): Promise<ApplicationListItem> {
    return firstValueFrom(this.http.post<ApplicationListItem>(`/api/applications/${id}/status`, { status, note }));
  }

  setArchived(id: number, archived: boolean): Promise<unknown> {
    return firstValueFrom(this.http.post(`/api/applications/${id}/${archived ? 'archive' : 'unarchive'}`, {}));
  }

  delete(id: number): Promise<unknown> {
    return firstValueFrom(this.http.delete(`/api/applications/${id}`));
  }
}
