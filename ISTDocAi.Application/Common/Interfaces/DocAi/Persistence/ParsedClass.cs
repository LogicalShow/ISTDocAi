using ISTDocAi.Domain.Dtos.DocAi;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
public sealed record ParsedClass(string? Name, double Confidence);
