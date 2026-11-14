using ISTDocAi.Domain.Entities.Public;
using Microsoft.Extensions.DependencyInjection;
using ISTDocAi.Domain.Common.Shared;

namespace ISTDocAi.Infrastructure.Data.Seeders.Public;

public class SettingSeeder : ISeeder
{
    public int Order => SeederOrder.CompanySettingSeeder;


    public async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        if (!context.Pub_Setting.Any())
        {
            var setting = new Pub_Setting()
            {
                Id = Extensions.GenerateEntityId(),
                CompanyNameEn = "ISTDocAi",
                CountryId = null,
                CompanyAddress = string.Empty,
                CompanyPhoneOne = string.Empty,
                CompanyPhoneTwo = null,
                CompanyEmail = null,
                CompanyFax = null,
                CompanyWebsite = null,
                DefaultCurrencyId = "USD",
                CompanyLogoPath = null,
                ReportHeaderPath = null,
                ReportFooterPath = null
            };

            await context.Pub_Setting.AddAsync(setting);

            await context.SaveChangesAsync();
        }
    }
}
