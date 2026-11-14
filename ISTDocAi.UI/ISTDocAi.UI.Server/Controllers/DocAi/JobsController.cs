using MediatR;
using ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Features.DocAi.Jobs.Commands.SubmitJob;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Base.DocAi;
using ISTDocAi.UI.Server.Services.DocAi;
namespace ISTDocAi.UI.Server.Controllers.DocAi;
[ApiController, Route("api/v2/jobs"), Authorize(AuthenticationSchemes = DocAiApiKeyHandler.SchemeName)]
public sealed class JobsController(ISender sender) : ControllerBase
{
    [HttpPost, DisableFormBinding, RequestSizeLimit(Contracts.Jobs.SubmitJobRequest.MaxRequestBytes)]
    public async Task<IActionResult> Submit(CancellationToken ct)
    {
        var path = Path.GetTempFileName();
        try
        {
            var file = await MultipartUpload.ReadAsync(Request, path, ct);
            var result = await sender.Send(new SubmitJobCommand((Consumer)HttpContext.Items["consumer"]!,
                file with { CorrelationId = DocAiTransport.Correlation(HttpContext) }), ct);
            return DocAiTransport.Success(HttpContext, result, result.Value.Accepted ? 202 : 200,
                result.Value.Accepted ? result.Value.StatusUrl : null, result.Value.Accepted ? 5 : null);
        }
        finally { System.IO.File.Delete(path); }
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var jobId)) throw IntakeException.Invalid();
        short? runNo = null;
        if (HttpContext.Request.Query.TryGetValue("runNo", out var value))
        {
            if (value.Count != 1 || !short.TryParse(value, out var number) || number < 1) throw IntakeException.Invalid();
            runNo = number;
        }
        var result = await sender.Send(new GetJobQuery((Consumer)HttpContext.Items["consumer"]!, jobId, runNo), ct);
        if (result.Value.Status is "Queued" or "Processing") HttpContext.Response.Headers.RetryAfter = "5";
        return DocAiTransport.Success(HttpContext, result);
    }
}
