using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
public sealed record PreparedSubmission(string Path, byte[] Hash, int Size, string FileName,
    string DocumentType, string? ShippingLine, string ClientRequestId, bool Reprocess, string CorrelationId);
