import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  RunScoringRequest,
  SaveScoringScenarioRequest,
  ScoringCatalog,
  ScoringResult,
  ScoringScenario,
} from '@frontend/models';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class ScoringApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getCatalog(): Observable<ScoringCatalog> {
    return this.http.get<ScoringCatalog>(
      `${this.apiBaseUrl}/api/scoring/catalog`,
    );
  }

  getProjectScenarios(projectId: string): Observable<ScoringScenario[]> {
    return this.http.get<ScoringScenario[]>(
      `${this.apiBaseUrl}/api/projects/${projectId}/scoring-scenarios`,
    );
  }

  getScenario(scenarioId: string): Observable<ScoringScenario> {
    return this.http.get<ScoringScenario>(
      `${this.apiBaseUrl}/api/scoring-scenarios/${scenarioId}`,
    );
  }

  createScenario(
    projectId: string,
    request: SaveScoringScenarioRequest,
  ): Observable<ScoringScenario> {
    return this.http.post<ScoringScenario>(
      `${this.apiBaseUrl}/api/projects/${projectId}/scoring-scenarios`,
      request,
    );
  }

  updateScenario(
    scenarioId: string,
    request: SaveScoringScenarioRequest,
  ): Observable<ScoringScenario> {
    return this.http.put<ScoringScenario>(
      `${this.apiBaseUrl}/api/scoring-scenarios/${scenarioId}`,
      request,
    );
  }

  duplicateScenario(scenarioId: string): Observable<ScoringScenario> {
    return this.http.post<ScoringScenario>(
      `${this.apiBaseUrl}/api/scoring-scenarios/${scenarioId}/duplicate`,
      {},
    );
  }

  runScoring(
    scenarioId: string,
    request: RunScoringRequest,
  ): Observable<ScoringResult[]> {
    return this.http.post<ScoringResult[]>(
      `${this.apiBaseUrl}/api/scoring-scenarios/${scenarioId}/score`,
      request,
    );
  }

  getResults(scenarioId: string): Observable<ScoringResult[]> {
    return this.http.get<ScoringResult[]>(
      `${this.apiBaseUrl}/api/scoring-scenarios/${scenarioId}/results`,
    );
  }
}
