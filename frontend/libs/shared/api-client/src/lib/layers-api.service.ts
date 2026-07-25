import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  LayerDefinition,
  LayerLegend,
  ProjectLayerPreference,
  UpdateProjectLayersRequest,
} from '@frontend/models';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from './api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class LayersApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getLayers(): Observable<LayerDefinition[]> {
    return this.http
      .get<LayerDefinition[]>(`${this.apiBaseUrl}/api/layers`)
      .pipe(
        map((layers) =>
          layers.map((layer) => ({
            ...layer,
            version: {
              ...layer.version,
              dataUrl: this.resolveDataUrl(layer.version.dataUrl),
            },
          })),
        ),
      );
  }

  getLayer(layerId: string): Observable<LayerDefinition> {
    return this.http
      .get<LayerDefinition>(
        `${this.apiBaseUrl}/api/layers/${layerId}`,
      )
      .pipe(
        map((layer) => ({
          ...layer,
          version: {
            ...layer.version,
            dataUrl: this.resolveDataUrl(layer.version.dataUrl),
          },
        })),
      );
  }

  getLegend(layerId: string): Observable<LayerLegend> {
    return this.http.get<LayerLegend>(
      `${this.apiBaseUrl}/api/layers/${layerId}/legend`,
    );
  }

  getProjectLayers(
    projectId: string,
  ): Observable<ProjectLayerPreference[]> {
    return this.http.get<ProjectLayerPreference[]>(
      `${this.apiBaseUrl}/api/projects/${projectId}/layers`,
    );
  }

  updateProjectLayers(
    projectId: string,
    request: UpdateProjectLayersRequest,
  ): Observable<ProjectLayerPreference[]> {
    return this.http.put<ProjectLayerPreference[]>(
      `${this.apiBaseUrl}/api/projects/${projectId}/layers`,
      request,
    );
  }

  private resolveDataUrl(dataUrl: string): string {
    if (/^https?:\/\//i.test(dataUrl)) {
      return dataUrl;
    }

    return `${this.apiBaseUrl}${dataUrl.startsWith('/') ? '' : '/'}${dataUrl}`;
  }
}
