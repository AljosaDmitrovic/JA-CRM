import { APIRequestContext, expect } from '@playwright/test';

/** Arranges data through the real API (same session cookie as the browser). */
export class Api {
  constructor(private readonly request: APIRequestContext) {}

  private readonly headers = { 'X-Requested-With': 'JaCrm' };

  async post<T>(url: string, data: unknown): Promise<T> {
    const response = await this.request.post(url, { data, headers: this.headers });
    expect(response.ok(), `${url}: ${await response.text()}`).toBeTruthy();
    return (await response.json()) as T;
  }

  async put<T>(url: string, data: unknown): Promise<T> {
    const response = await this.request.put(url, { data, headers: this.headers });
    expect(response.ok(), `${url}: ${await response.text()}`).toBeTruthy();
    return (await response.json()) as T;
  }

  async get<T>(url: string): Promise<T> {
    const response = await this.request.get(url);
    expect(response.ok(), `${url}: ${await response.text()}`).toBeTruthy();
    return (await response.json()) as T;
  }

  company(name: string, extra: Record<string, unknown> = {}) {
    return this.post<{ id: number; name: string }>('/api/companies', { name, ...extra });
  }

  vacancy(title: string, companyId: number, extra: Record<string, unknown> = {}) {
    return this.post<{ id: number; title: string }>('/api/vacancies', { title, companyId, ...extra });
  }

  application(title: string, company: string, status = 'Interested') {
    return this.post<{ id: number }>('/api/applications', { newVacancy: { title, newCompany: { name: company } }, status, priority: 'Medium' });
  }

  getApplication(id: number) {
    return this.get<{ status: string; history: { fromStatus: string | null; toStatus: string }[]; snapshot: { jobTitle: string } }>(
      `/api/applications/${id}`,
    );
  }
}

/** A unique suffix, so tests never collide with each other's data. */
export const unique = () => Math.random().toString(36).slice(2, 7);
