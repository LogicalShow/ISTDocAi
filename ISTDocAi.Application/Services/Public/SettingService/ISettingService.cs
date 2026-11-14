using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using System.Linq.Expressions;
using ISTDocAi.Application.Services.Public.SettingService.Contracts;

namespace ISTDocAi.Application.Services.Public.SettingService;

public interface ISettingService
{
    Task<Result<SettingResponse>> GetSettingAsync(CancellationToken cancellationToken);
    Task<Result<UpdateSettingResponse>> UpdateSetting(UpdateSetting request, CancellationToken cancellationToken);
    Task<Result<EmailConfiguration>> GetEmailConfigurationAsync(CancellationToken cancellationToken);
    Task<bool> IsEmailEnabledAsync(CancellationToken cancellationToken);

    Task<TValue?> GetSettingValueAsync<TValue>(Expression<Func<Pub_Setting, TValue>> selector, CancellationToken cancellationToken);
}
