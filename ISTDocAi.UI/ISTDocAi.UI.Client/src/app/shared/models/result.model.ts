import { ApiError } from '../../core/models/api-error.model';

export interface AggregateError extends ApiError {
  Errors: ApiError[];
}

// Result wrapper - matches the C# Result<T> pattern
export interface Result<T> {
  Value: T;
  IsSuccess: boolean;
  IsFailure: boolean;
  Error: ApiError;
  HasValidationErrors: boolean;
  ValidationErrors: ApiError[];
}
