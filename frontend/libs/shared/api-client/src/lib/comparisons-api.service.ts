import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  ComparisonSummary,
  CreateComparisonRequest,
  SiteComparison,
} from '@frontend/models';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class ComparisonsApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  createComparison(
    projectId: string,
    request: CreateComparisonRequest,
  ): Observable<SiteComparison> {
    return this.http.post<SiteComparison>(
      `${this.apiBaseUrl}/api/projects/${projectId}/comparisons`,
      request,
    );
  }

  getComparison(comparisonId: string): Observable<SiteComparison> {
    return this.http.get<SiteComparison>(
      `${this.apiBaseUrl}/api/comparisons/${comparisonId}`,
    );
  }

  getProjectComparisons(projectId: string): Observable<ComparisonSummary[]> {
    return this.http.get<ComparisonSummary[]>(
      `${this.apiBaseUrl}/api/projects/${projectId}/comparisons`,
    );
  }
}
