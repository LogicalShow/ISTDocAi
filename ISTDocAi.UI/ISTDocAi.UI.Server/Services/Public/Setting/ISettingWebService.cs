using ISTDocAi.Application.Services.Public.SettingService.Contracts;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.UI.Server.Helpers.Models;

namespace ISTDocAi.UI.Server.Services.Public.Setting
{
    public interface ISettingWebService
    {
        Task<Result<SettingResponse>> GetSettingAsync(CancellationToken cancellationToken);

        Task<Result<UpdateSettingResponse>> UpdateSetting(UpdateSettingRequest request, CancellationToken cancellationToken);

        Task<Result<ImageResponse>> GetImage(ImageType imageType, CancellationToken cancellationToken);
    }
}
