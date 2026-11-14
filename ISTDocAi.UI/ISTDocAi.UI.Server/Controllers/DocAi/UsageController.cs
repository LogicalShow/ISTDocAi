using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Features.DocAi.Usage.Queries.GetUsage;
using ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetTopUpCycles;
using ISTDocAi.Application.Features.DocAi.TopUps.Queries.GetCycleDocuments;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Base.DocAi;
namespace ISTDocAi.UI.Server.Controllers.DocAi;
[ApiController, Route("api/v2/usage"), Authorize(AuthenticationSchemes = DocAiApiKeyHandler.SchemeName)]
public sealed class UsageController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var months = 6;
        if (HttpContext.Request.Query.TryGetValue("months", out var value))
        {
            if (value.Count != 1 || !int.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out months) || months is < 1 or > 24)
                throw IntakeException.Invalid();
        }
        return DocAiTransport.Success(HttpContext, await sender.Send(new GetUsageQuery(ConsumerId(HttpContext), months), ct));
    }
    [HttpGet("topups")]
    public async Task<IActionResult> Cycles(CancellationToken ct)
    {
        ValidateQuery(HttpContext);
        return DocAiTransport.Success(HttpContext, await sender.Send(new GetTopUpCyclesQuery(ConsumerId(HttpContext), null), ct));
    }
    [HttpGet("topups/{grantId}")]
    public async Task<IActionResult> Cycle(string grantId, CancellationToken ct)
    {
        ValidateQuery(HttpContext);
        return DocAiTransport.Success(HttpContext, await sender.Send(new GetTopUpCyclesQuery(ConsumerId(HttpContext), GrantId(grantId)), ct));
    }
    [HttpGet("topups/{grantId}/documents")]
    public async Task<IActionResult> Documents(string grantId, CancellationToken ct)
    {
        ValidateQuery(HttpContext, "limit", "afterChargeId");
        var id = GrantId(grantId);
        var limit = HttpContext.Request.Query.TryGetValue("limit", out var size) ? Positive(size[0]!) : 100;
        if (limit > 500) throw IntakeException.Invalid();
        long? after = HttpContext.Request.Query.TryGetValue("afterChargeId", out var cursor) ? Positive(cursor[0]!) : null;
        return DocAiTransport.Success(HttpContext, await sender.Send(new GetCycleDocumentsQuery(ConsumerId(HttpContext), id, (int)limit, after), ct));
    }
    private static short ConsumerId(HttpContext context) => ((Consumer)context.Items["consumer"]!).ConsumerId;
    private static long GrantId(string value) => value == "prelude" ? 0 : Positive(value);
    private static long Positive(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
        ? n : throw IntakeException.Invalid();
    private static void ValidateQuery(HttpContext context, params string[] allowed)
    {
        context.Response.Headers.CacheControl = "no-store";
        foreach (var pair in context.Request.Query)
            if (pair.Value.Count != 1 || !allowed.Contains(pair.Key, StringComparer.Ordinal)) throw IntakeException.Invalid();
    }
}
