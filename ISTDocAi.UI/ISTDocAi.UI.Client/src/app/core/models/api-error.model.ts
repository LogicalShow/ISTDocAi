export const ApiErrorType = {
  None: 'None',
  Failure: 'Failure',
  Validation: 'Validation',
  BusinessRule: 'BusinessRule',
  NotFound: 'NotFound',
  Unauthorized: 'Unauthorized',
  Forbidden: 'Forbidden',
  Conflict: 'Conflict',
  Unexpected: 'Unexpected',
  RateLimited: 'RateLimited'
} as const;

export type ApiErrorType = typeof ApiErrorType[keyof typeof ApiErrorType];

const apiErrorTypes = new Set<string>(Object.values(ApiErrorType));

export function isApiErrorType(value: unknown): value is ApiErrorType {
  return typeof value === 'string' && apiErrorTypes.has(value);
}

export interface ApiError {
  Code: string;
  Message: string;
  Type: ApiErrorType;
}

export interface ApiResultFailure {
  IsSuccess?: false;
  IsFailure?: true;
  Error: ApiError;
  Errors?: ApiError[];
  ValidationErrors?: ApiError[];
}

export interface ApiValidationResponse extends ApiResultFailure {
  Errors: ApiError[];
}
