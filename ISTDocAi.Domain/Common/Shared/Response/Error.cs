using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Domain.Common.Shared.Response
{
    public class Error : IEquatable<Error>
    {
        public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
        public static readonly Error NullValue = Failure(
            "Error.NullValue",
            "The specified result value is null.");

        protected Error(string code, string message, ErrorType type)
        {
            Code = code;
            Message = message;
            Type = type;
        }

        public string Code { get; }

        public string Message { get; }

        public ErrorType Type { get; }

        public Error WithMessage(string message) => new(Code, message, Type);

        public static Error Failure(string code, string message) =>
            new(code, message, ErrorType.Failure);

        public static Error Validation(string code, string message) =>
            new(code, message, ErrorType.Validation);

        public static Error BusinessRule(string code, string message) =>
            new(code, message, ErrorType.BusinessRule);

        public static Error NotFound(string code, string message) =>
            new(code, message, ErrorType.NotFound);

        public static Error Unauthorized(string code, string message) =>
            new(code, message, ErrorType.Unauthorized);

        public static Error Forbidden(string code, string message) =>
            new(code, message, ErrorType.Forbidden);

        public static Error Conflict(string code, string message) =>
            new(code, message, ErrorType.Conflict);

        public static Error Unexpected(string code, string message) =>
            new(code, message, ErrorType.Unexpected);

        public static Error RateLimited(string code, string message) =>
            new(code, message, ErrorType.RateLimited);

        public static implicit operator string(Error error) => error.Code;

        public static bool operator ==(Error? a, Error? b)
        {
            if (a is null && b is null)
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(Error? a, Error? b) => !(a == b);

        public virtual bool Equals(Error? other)
        {
            if (other is null)
            {
                return false;
            }

            return Code == other.Code && Message == other.Message && Type == other.Type;
        }

        public override bool Equals(object? obj) => obj is Error error && Equals(error);

        public override int GetHashCode() => HashCode.Combine(Code, Message, Type);

        public override string ToString() => Code;
    }
}
