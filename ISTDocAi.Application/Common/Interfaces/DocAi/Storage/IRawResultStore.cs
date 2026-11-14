using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Storage;

public interface IRawResultStore
{
    Task<string> SaveAsync(Guid jobId, short runNo, long operationId, string rawJson, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);
    Task<string> ReadAsync(string blobPath, CancellationToken ct);
}


// Opaque local preparation. Reserve before recording Sending; submit once immediately afterwards.
