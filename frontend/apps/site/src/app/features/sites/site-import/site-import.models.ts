import { SiteImportResult } from '@frontend/models';

export interface SiteImportCompletedEvent {
  targetProjectId: string;
  result: SiteImportResult;
}
