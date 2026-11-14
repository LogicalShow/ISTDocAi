using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Features.DocAi.Catalog.Queries.GetCatalog;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Base.DocAi;
namespace ISTDocAi.UI.Server.Controllers.DocAi;
[ApiController, Route("api/v2/catalog"), Authorize(AuthenticationSchemes = DocAiApiKeyHandler.SchemeName)]
public sealed class CatalogController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => DocAiTransport.Success(HttpContext,
        await sender.Send(new GetCatalogQuery((Consumer)HttpContext.Items["consumer"]!), ct));
}
