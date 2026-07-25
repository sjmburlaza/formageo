import { HttpErrorResponse } from '@angular/common/http';

export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    const responseBody = error.error as
      | { error?: unknown; title?: unknown }
      | string
      | null;

    if (
      typeof responseBody === 'object' &&
      responseBody !== null &&
      typeof responseBody.error === 'string'
    ) {
      return responseBody.error;
    }

    if (
      typeof responseBody === 'object' &&
      responseBody !== null &&
      typeof responseBody.title === 'string'
    ) {
      return responseBody.title;
    }

    if (typeof responseBody === 'string' && responseBody.trim()) {
      return responseBody;
    }

    if (error.status === 0) {
      return 'The API could not be reached. Check that the backend is running.';
    }
  }

  return fallback;
}
