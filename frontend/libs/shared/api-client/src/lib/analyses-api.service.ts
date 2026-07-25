import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  AnalysisDefinition,
  AnalysisRun,
  CreateAnalysisRequest,
} from '@frontend/models';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class AnalysesApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getCatalog(): Observable<AnalysisDefinition[]> {
    return this.http.get<AnalysisDefinition[]>(
      `${this.apiBaseUrl}/api/analyses/catalog`,
    );
  }

  createAnalysis(
    siteId: string,
    request: CreateAnalysisRequest,
  ): Observable<AnalysisRun> {
    return this.http.post<AnalysisRun>(
      `${this.apiBaseUrl}/api/sites/${siteId}/analyses`,
      request,
    );
  }

  getAnalysis(analysisId: string): Observable<AnalysisRun> {
    return this.http.get<AnalysisRun>(
      `${this.apiBaseUrl}/api/analyses/${analysisId}`,
    );
  }

  getSiteAnalyses(siteId: string): Observable<AnalysisRun[]> {
    return this.http.get<AnalysisRun[]>(
      `${this.apiBaseUrl}/api/sites/${siteId}/analyses`,
    );
  }

  cancelAnalysis(analysisId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiBaseUrl}/api/analyses/${analysisId}`,
    );
  }
}
