using ISTDocAi.Application.Services.Public.SettingService.Contracts;
using ISTDocAi.Domain.Entities.Public;
using Mapster;

namespace ISTDocAi.Application.Common.Mappings.Mapster.Public;

public sealed class SettingMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<UpdateSetting, Pub_Setting>()
            .Ignore(destination => destination.CompanyLogoPath)
            .Ignore(destination => destination.ReportHeaderPath)
            .Ignore(destination => destination.ReportFooterPath)
            .Ignore(destination => destination.CompanyLogoHash)
            .Ignore(destination => destination.ReportHeaderHash)
            .Ignore(destination => destination.ReportFooterHash)
            .Ignore(destination => destination.CompanyLogoBinaryData)
            .Ignore(destination => destination.ReportHeaderBinaryData)
            .Ignore(destination => destination.ReportFooterBinaryData)
            .Ignore(destination => destination.CompanyLogoContentType)
            .Ignore(destination => destination.ReportHeaderContentType)
            .Ignore(destination => destination.ReportFooterContentType);

        config.NewConfig<Pub_Setting, SettingResponse>();
    }
}
