using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;

namespace ISTDocAi.Application.Services.DocAi.Webhooks;

public sealed record WebhookBody(Guid EventId, string EventType, DateTimeOffset OccurredAtUtc, GetJobResponse Job);

public static class WebhookBodyProjector
{
    public static byte[] Project(JsonObject payload, string? resultJson, JsonSerializerOptions options)
    {
        var resultType = payload["resultType"]?.GetValue<string>();
        var canonical = resultType is not null || payload.ContainsKey("requestedDocumentType");
        var result = resultJson is null || !canonical ? null : CanonicalResultCodec.Deserialize(resultJson, resultType);
        var job = new JobResponse {
            JobId = payload["jobId"]?.GetValue<Guid>() ?? Guid.Empty,
            RunNo = payload["runNo"]?.GetValue<int>() ?? 0,
            ClientRequestId = payload["clientRequestId"]?.GetValue<string>() ?? "",
            RunReason = payload["runReason"]?.GetValue<string>() ?? "New",
            Changes = payload["changes"]?.Deserialize<RunChanges>(ContractJson.Options),
            Status = payload["status"]?.GetValue<string>() ?? "",
            Error = payload["error"]?.Deserialize<JobError>(ContractJson.Options),
            Result = result,
            RequestedDocumentType = payload["requestedDocumentType"]?.GetValue<string>(),
            DocumentType = payload["documentType"]?.GetValue<string>() ?? result?.DocumentTypeCode,
            ResultType = resultType ?? (result is null ? null : CanonicalResultCodec.TypeOf(result))
        };
        var body = new WebhookBody(
            payload["eventId"]?.GetValue<Guid>() ?? Guid.Empty,
            payload["eventType"]?.GetValue<string>() ?? "",
            payload["occurredAtUtc"]?.GetValue<DateTimeOffset>() ?? default,
            GetJobProjector.Project(job));
        if (canonical || resultJson is null) return JsonSerializer.SerializeToUtf8Bytes(body, options);
        // Legacy payloads deliberately bypass canonical decoding, matching v1's raw insertion.
        var node = JsonSerializer.SerializeToNode(body, options)!.AsObject();
        node["Job"]!["Result"] = JsonNode.Parse(resultJson);
        return JsonSerializer.SerializeToUtf8Bytes(node, options);
    }
}
