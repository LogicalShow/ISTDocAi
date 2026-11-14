using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Domain.Common.Shared.Response
{
    public class Result<TValue> : Result
    {
        private readonly TValue? _value;

        protected internal Result(TValue? value, bool isSuccess, Error error)
            : base(isSuccess, error) =>
            _value = value;

        public TValue Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException(
                $"Cannot access the value of a failed result. Error: {Error.Code}");

        public static implicit operator Result<TValue>(TValue? value) => Create(value);

        public static new Result<TValue> Failure(params Error[] errors) =>
           errors.Length == 1 ? Failure<TValue>(errors[0]) : new Result<TValue>(default, false, new AggregateError(errors));

        // Add a method to get typed validation errors
        public IEnumerable<TError> GetValidationErrors<TError>() where TError : Error =>
            ValidationErrors.OfType<TError>();
    }
}
