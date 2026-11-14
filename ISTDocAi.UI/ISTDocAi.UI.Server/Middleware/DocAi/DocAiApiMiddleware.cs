using ISTDocAi.UI.Server.Services.DocAi;
using System.Globalization;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Base.DocAi;
using Microsoft.AspNetCore.Authentication;
namespace ISTDocAi.UI.Server.Middleware.DocAi;
public sealed class DocAiApiMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<DocAiApiMiddleware> logger)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v2")) { await next(context); return; }
        // Select display culture only. All non-display intake work stays invariant.
        var oldCulture = CultureInfo.CurrentCulture;
        var oldUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = await DocAiRequestCulture.SelectAsync(context);
        try
        {
            var result = await context.AuthenticateAsync(DocAiApiKeyHandler.SchemeName);
            if (!result.Succeeded) { await context.ChallengeAsync(DocAiApiKeyHandler.SchemeName); return; }
            context.User = result.Principal!;
            await next(context);
        }
        catch (IntakeException e) { await DocAiTransport.Failure(context, e, context.RequestAborted); }
        catch (BadHttpRequestException e) { await DocAiTransport.Failure(context, e.StatusCode == 413
            ? new(413, "FILE_TOO_LARGE", "File too large", "The file exceeds 20 MB.") : IntakeException.Invalid(), context.RequestAborted); }
        catch (InvalidDataException) { await DocAiTransport.Failure(context, IntakeException.Invalid(), context.RequestAborted); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception e)
        {
            logger.LogError("DocAi request failed with INTERNAL_ERROR; exception type {ExceptionType}; stack trace {StackTrace}",
                e.GetType().FullName, e.StackTrace);
            await DocAiTransport.Failure(context, DocAiTransport.Internal(), context.RequestAborted);
        }
        finally { CultureInfo.CurrentCulture = oldCulture; CultureInfo.CurrentUICulture = oldUi; }
    }
}
