using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Domain.Common.Shared.Response
{
    public class AggregateError : Error
    {
        public IEnumerable<Error> Errors { get; }

        public AggregateError(IEnumerable<Error> errors)
            : this(errors.ToArray())
        {
        }

        public AggregateError(params Error[] errors)
            : base("AggregateError", "Multiple errors occurred.", ResolveType(errors))
        {
            Errors = errors;
        }

        private static ErrorType ResolveType(IReadOnlyCollection<Error> errors) =>
            errors.Count > 0 && errors.All(error => error.Type == errors.First().Type)
                ? errors.First().Type
                : ErrorType.Failure;

        public override string ToString()
        {
            var errorMessages = Errors.Select(e => e.ToString());
            return $"{base.ToString()}: {string.Join(", ", errorMessages)}";
        }
    }
}
