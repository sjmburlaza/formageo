import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CreateSiteRequest, Site } from '@frontend/models';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class SitesApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  createSite(projectId: string, request: CreateSiteRequest): Observable<Site> {
    return this.http.post<Site>(
      `${this.apiBaseUrl}/api/projects/${projectId}/sites`,
      request,
    );
  }

  getProjectSites(projectId: string): Observable<Site[]> {
    return this.http.get<Site[]>(
      `${this.apiBaseUrl}/api/projects/${projectId}/sites`,
    );
  }

  getSite(siteId: string): Observable<Site> {
    return this.http.get<Site>(`${this.apiBaseUrl}/api/sites/${siteId}`);
  }

  deleteSite(siteId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiBaseUrl}/api/sites/${siteId}`);
  }
}
