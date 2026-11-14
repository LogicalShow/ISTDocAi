using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.Iso6346Service;
public interface IIso6346Service
{
    string? Normalize(string? number);
    bool IsValid(string? number);
}
