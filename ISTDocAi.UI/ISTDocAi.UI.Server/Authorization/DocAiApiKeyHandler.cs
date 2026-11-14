using System.Security.Claims;
using System.Text.Encodings.Web;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.UI.Server.Base.DocAi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace ISTDocAi.UI.Server.Authorization;
public sealed class DocAiApiKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DocAiApiKey";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Path.StartsWithSegments("/api/v2")) return AuthenticateResult.NoResult();
        var keys = Request.Headers["X-Api-Key"];
        Consumer? consumer = null;
        if (keys.Count == 1 && !string.IsNullOrWhiteSpace(keys[0]) && keys[0]!.Length <= 1024)
            consumer = await Context.RequestServices.GetRequiredService<IConsumerDirectory>().AuthenticateAsync(keys[0]!, Context.RequestAborted);
        if (consumer is null) return AuthenticateResult.Fail("UNAUTHORIZED");
        Context.Items["consumer"] = consumer;
        try
        {
            Context.RequestServices.GetService<ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks.IConsumerWebhookEvents>()
                ?.NotifyAlive(consumer.ConsumerId);
        }
        catch (Exception) { Logger.LogWarning("{event}", "webhook.consumer_alive_failed"); }
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, consumer.ConsumerId.ToString(System.Globalization.CultureInfo.InvariantCulture))], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) => DocAiTransport.Failure(Context,
        new(401, "UNAUTHORIZED", "Unauthorized", "A valid API key is required."), Context.RequestAborted);
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) => DocAiTransport.Failure(Context,
        new(403, "FORBIDDEN", "Forbidden", "Access is forbidden."), Context.RequestAborted);
}
