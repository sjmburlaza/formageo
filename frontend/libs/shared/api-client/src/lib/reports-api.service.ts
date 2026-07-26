import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  CreateReportRequest,
  GeneratedReport,
  ReportPreview,
} from '@frontend/models';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class ReportsApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  previewSiteReport(
    siteId: string,
    request: CreateReportRequest,
  ): Observable<ReportPreview> {
    return this.http.post<ReportPreview>(
      `${this.apiBaseUrl}/api/sites/${siteId}/reports/preview`,
      request,
    );
  }

  createSiteReport(
    siteId: string,
    request: CreateReportRequest,
  ): Observable<GeneratedReport> {
    return this.http.post<GeneratedReport>(
      `${this.apiBaseUrl}/api/sites/${siteId}/reports`,
      request,
    );
  }

  getSiteReports(siteId: string): Observable<GeneratedReport[]> {
    return this.http.get<GeneratedReport[]>(
      `${this.apiBaseUrl}/api/sites/${siteId}/reports`,
    );
  }

  previewComparisonReport(
    comparisonId: string,
    request: CreateReportRequest,
  ): Observable<ReportPreview> {
    return this.http.post<ReportPreview>(
      `${this.apiBaseUrl}/api/comparisons/${comparisonId}/reports/preview`,
      request,
    );
  }

  createComparisonReport(
    comparisonId: string,
    request: CreateReportRequest,
  ): Observable<GeneratedReport> {
    return this.http.post<GeneratedReport>(
      `${this.apiBaseUrl}/api/comparisons/${comparisonId}/reports`,
      request,
    );
  }

  getComparisonReports(comparisonId: string): Observable<GeneratedReport[]> {
    return this.http.get<GeneratedReport[]>(
      `${this.apiBaseUrl}/api/comparisons/${comparisonId}/reports`,
    );
  }

  createProjectArchive(
    projectId: string,
    request: CreateReportRequest,
  ): Observable<GeneratedReport> {
    return this.http.post<GeneratedReport>(
      `${this.apiBaseUrl}/api/projects/${projectId}/reports`,
      request,
    );
  }

  downloadReport(reportId: string): Observable<Blob> {
    return this.http.get(
      `${this.apiBaseUrl}/api/reports/${reportId}/download`,
      {
        responseType: 'blob',
      },
    );
  }
}
