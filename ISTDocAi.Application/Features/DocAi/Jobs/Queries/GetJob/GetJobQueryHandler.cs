using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Domain.Common.Shared.Response;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Queries.GetJob;
public sealed class GetJobQueryHandler(ISTDocAi.Application.Services.DocAi.Intake.GetJob service, ISTDocAi.Application.Common.Localization.IMessageLocalizer localizer) : IQueryHandler<GetJobQuery, GetJobResponse>
{
    public async Task<Result<GetJobResponse>> Handle(GetJobQuery request, CancellationToken cancellationToken)
    {
        var b = await service.ExecuteAsync(request.JobId, request.Consumer, request.RunNo, cancellationToken);
        var error = b.Error;
        if (error is not null && System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar")
            error = error with { Message = localizer.Localize(error.Code, error.Message) };
        return Result.Success(GetJobProjector.Project(b with { Error = error }));
    }
}
