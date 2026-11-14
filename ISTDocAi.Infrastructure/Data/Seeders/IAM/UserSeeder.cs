using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ISTDocAi.Domain.Common.Constants.IAM;
using ISTDocAi.Infrastructure.IAM.Models;

namespace ISTDocAi.Infrastructure.Data.Seeders.IAM;

public class UserSeeder : ISeeder
{
    public int Order => SeederOrder.UserSeeder;
    public async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();
        if (await context.Users.AnyAsync()) return;
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password) && !configuration.GetValue<bool>("TemplateEndpoints:Enabled"))
        {
            serviceProvider.GetRequiredService<ILogger<UserSeeder>>().LogInformation("{event}", "database.iam_admin_skipped");
            return;
        }
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set Bootstrap:AdminPassword before starting a fresh database.");
        var user = new IAM_User
        {
            Id = SeededUserIds.Admin,
            UserName = configuration["Bootstrap:AdminUserName"] ?? "admin",
            Email = configuration["Bootstrap:AdminEmail"] ?? "admin@example.test",
            EmailConfirmed = true,
            FullName = "Administrator",
            IsActive = true,
            IsAdministrator = true
        };
        var result = await serviceProvider.GetRequiredService<UserManager<IAM_User>>().CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
