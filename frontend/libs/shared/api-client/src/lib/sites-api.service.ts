import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  CreateSiteRequest,
  Site,
  SiteImportOptions,
  SiteImportResult,
  SiteSummary,
  UpdateSiteBoundaryRequest,
  UpdateSiteRequest,
} from '@frontend/models';
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

  importSites(
    projectId: string,
    file: File,
    options: SiteImportOptions,
  ): Observable<SiteImportResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('namePattern', options.namePattern);
    formData.append('mode', options.mode);

    for (const featureIndex of options.selectedFeatureIndexes) {
      formData.append('selectedFeatureIndexes', featureIndex.toString());
    }

    return this.http.post<SiteImportResult>(
      `${this.apiBaseUrl}/api/projects/${projectId}/site-imports`,
      formData,
    );
  }

  getSite(siteId: string): Observable<Site> {
    return this.http.get<Site>(`${this.apiBaseUrl}/api/sites/${siteId}`);
  }

  getSiteSummary(siteId: string): Observable<SiteSummary> {
    return this.http.get<SiteSummary>(
      `${this.apiBaseUrl}/api/sites/${siteId}/summary`,
    );
  }

  updateSite(siteId: string, request: UpdateSiteRequest): Observable<Site> {
    return this.http.patch<Site>(
      `${this.apiBaseUrl}/api/sites/${siteId}`,
      request,
    );
  }

  updateBoundary(
    siteId: string,
    request: UpdateSiteBoundaryRequest,
  ): Observable<Site> {
    return this.http.put<Site>(
      `${this.apiBaseUrl}/api/sites/${siteId}/boundary`,
      request,
    );
  }

  archiveSite(siteId: string): Observable<Site> {
    return this.http.post<Site>(
      `${this.apiBaseUrl}/api/sites/${siteId}/archive`,
      {},
    );
  }

  restoreSite(siteId: string): Observable<Site> {
    return this.http.post<Site>(
      `${this.apiBaseUrl}/api/sites/${siteId}/restore`,
      {},
    );
  }

  deleteSite(siteId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiBaseUrl}/api/sites/${siteId}`);
  }

  exportSiteGeoJson(siteId: string): Observable<Blob> {
    return this.http.get(
      `${this.apiBaseUrl}/api/sites/${siteId}/export`,
      {
        params: { format: 'geojson' },
        responseType: 'blob',
      },
    );
  }
}
