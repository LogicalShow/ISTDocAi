import { HttpErrorResponse } from '@angular/common/http';
import {
  from,
  map,
  mergeMap,
  Observable,
  OperatorFunction,
  throwError
} from 'rxjs';
import {
  ApiError,
  ApiResultFailure,
  isApiErrorType
} from '../../core/models/api-error.model';
import { Result } from '../models/result.model';

type UnknownRecord = Record<string, unknown>;

export function unwrapResult<T>(): OperatorFunction<Result<T>, T> {
  return map(result => {
    if (!result.IsSuccess) {
      throw result;
    }

    return result.Value;
  });
}

export function extractApiFailure(source: unknown): ApiResultFailure | null {
  const candidate = source instanceof HttpErrorResponse
    ? source.error
    : source;

  if (!isRecord(candidate)) {
    return null;
  }

  if (isApiError(candidate)) {
    return { Error: candidate };
  }

  if (!isApiError(candidate['Error'])) {
    return null;
  }

  return {
    IsSuccess: false,
    IsFailure: true,
    Error: candidate['Error'],
    Errors: readErrors(candidate['Errors']),
    ValidationErrors: readErrors(candidate['ValidationErrors'])
  };
}

export function rethrowApiError(source: unknown): Observable<never> {
  return from(normalizeApiError(source)).pipe(
    mergeMap(error => throwError(() => error))
  );
}

async function normalizeApiError(source: unknown): Promise<unknown> {
  if (!(source instanceof HttpErrorResponse) || !(source.error instanceof Blob)) {
    return source;
  }

  const contentType = source.error.type.toLowerCase();
  if (!contentType.includes('json')) {
    return source;
  }

  try {
    const error = JSON.parse(await source.error.text());
    return new HttpErrorResponse({
      error,
      headers: source.headers,
      status: source.status,
      statusText: source.statusText,
      url: source.url ?? undefined
    });
  } catch {
    return source;
  }
}

function readErrors(value: unknown): ApiError[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const errors = value.filter(isApiError);
  return errors.length > 0 ? errors : undefined;
}

function isApiError(value: unknown): value is ApiError {
  if (!isRecord(value)) {
    return false;
  }

  return typeof value['Code'] === 'string'
    && typeof value['Message'] === 'string'
    && isApiErrorType(value['Type']);
}

function isRecord(value: unknown): value is UnknownRecord {
  return typeof value === 'object' && value !== null;
}
