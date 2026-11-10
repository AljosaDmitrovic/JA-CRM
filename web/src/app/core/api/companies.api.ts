import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CompanyDetail, CompanyListItem, CompanyOption, CompanyRequest, DuplicateCompany, Paged, QueryParams } from '../models';
import { toParams } from './http-params';

@Injectable({ providedIn: 'root' })
export class CompaniesApi {
  private readonly http = inject(HttpClient);

  list(query: QueryParams): Promise<Paged<CompanyListItem>> {
    return firstValueFrom(this.http.get<Paged<CompanyListItem>>('/api/companies', { params: toParams(query) }));
  }

  options(includeArchived = false): Promise<CompanyOption[]> {
    return firstValueFrom(this.http.get<CompanyOption[]>('/api/companies/options', { params: toParams({ includeArchived }) }));
  }

  get(id: number): Promise<CompanyDetail> {
    return firstValueFrom(this.http.get<CompanyDetail>(`/api/companies/${id}`));
  }

  duplicates(name: string, website: string | null, excludeId: number | null): Promise<DuplicateCompany[]> {
    return firstValueFrom(
      this.http.get<DuplicateCompany[]>('/api/companies/duplicates', { params: toParams({ name, website, excludeId }) }),
    );
  }

  create(request: CompanyRequest): Promise<CompanyDetail> {
    return firstValueFrom(this.http.post<CompanyDetail>('/api/companies', request));
  }

  update(id: number, request: CompanyRequest): Promise<CompanyDetail> {
    return firstValueFrom(this.http.put<CompanyDetail>(`/api/companies/${id}`, request));
  }

  setArchived(id: number, archived: boolean): Promise<unknown> {
    return firstValueFrom(this.http.post(`/api/companies/${id}/${archived ? 'archive' : 'unarchive'}`, {}));
  }

  delete(id: number): Promise<unknown> {
    return firstValueFrom(this.http.delete(`/api/companies/${id}`));
  }
}
