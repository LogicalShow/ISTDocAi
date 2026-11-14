namespace ISTDocAi.Domain.Common.Shared.Response;

public enum ErrorType
{
    None = 0,
    Failure = 1,
    Validation = 2,
    BusinessRule = 3,
    NotFound = 4,
    Unauthorized = 5,
    Forbidden = 6,
    Conflict = 7,
    Unexpected = 8,
    RateLimited = 9
}
