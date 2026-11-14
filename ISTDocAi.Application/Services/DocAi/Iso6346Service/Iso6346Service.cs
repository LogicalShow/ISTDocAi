using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.Iso6346Service;
public sealed class Iso6346Service : IIso6346Service
{
    /// <summary>Remove printed separators only when the result has the complete ISO container-number shape.</summary>
    public string? Normalize(string? number)
    {
        if (number is null)
            return null;
        var candidate = new string (number.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '.' or '/')).ToArray()).ToUpperInvariant();
        return candidate.Length == 11 && candidate.Take(3).All(c => c is >= 'A' and <= 'Z') && candidate[3] is 'U' or 'J' or 'Z' && candidate.Skip(4).All(c => c is >= '0' and <= '9') ? candidate : number.Trim();
    }

    /// <summary>Strict canonical form: three ASCII owner letters, U/J/Z, six digits and the check digit.</summary>
    public bool IsValid(string? number)
    {
        if (number is not { Length: 11 })
            return false;
        for (var i = 0; i < 3; i++)
            if (number[i] is < 'A' or > 'Z')
                return false;
        if (number[3] is not ('U' or 'J' or 'Z'))
            return false;
        for (var i = 4; i < 11; i++)
            if (number[i] is < '0' or > '9')
                return false;
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var value = number[i] is >= 'A' and <= 'Z' ? number[i] - 'A' + 10 : number[i] - '0';
            if (number[i] is >= 'A' and <= 'Z')
                value += (value - 1) / 10; // skip 11, 22, 33
            sum += value * (1 << i);
        }

        return sum % 11 % 10 == number[10] - '0';
    }
}
