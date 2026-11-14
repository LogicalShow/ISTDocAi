using ISTDocAi.Application.Common.Contracts.DocAi.V1;

namespace ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;

public static class GetJobProjector
{
    public static GetJobResponse Project(JobResponse b) => new(b.JobId, b.RunNo, b.ClientRequestId,
        b.RunReason, b.Changes, b.Status, b.Error, b.Result, b.RequestedDocumentType, b.DocumentType, b.ResultType);
}
