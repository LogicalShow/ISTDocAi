using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Domain.Common.Shared.Response
{
    public interface IValidationResult
    {
        public static readonly Error ValidationError = Error.Validation(
            ErrorCodes.ValidationFailed,
            "A validation problem occurred.");

        Error[] Errors { get; }
    }
}
