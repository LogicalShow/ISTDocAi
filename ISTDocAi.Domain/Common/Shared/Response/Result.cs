using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Domain.Common.Shared.Response
{
    public class Result
    {
        protected internal Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None)
            {
                throw new InvalidOperationException(
                    "A successful result cannot contain an error.");
            }

            if (!isSuccess && error == Error.None)
            {
                throw new InvalidOperationException(
                    "A failed result must contain an error.");
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public Error Error { get; }

        public static Result Success() => new(true, Error.None);

        public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

        public static Result Failure(Error error) => new(false, error);

        public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

        public static Result<TValue> Create<TValue>(TValue? value) => value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

        public bool HasValidationErrors => this is IValidationResult || Error is AggregateError;

        // Add a property to get the validation errors
        public IEnumerable<Error> ValidationErrors => this switch
        {
            IValidationResult validationResult => validationResult.Errors,
            { Error: AggregateError aggregateError } => aggregateError.Errors,
            _ => []
        };

        // Add methods to create results with validation errors
        public static Result Failure(params Error[] errors) =>
            errors.Length == 1 ? Failure(errors[0]) : new Result(false, new AggregateError(errors));
    }
}
