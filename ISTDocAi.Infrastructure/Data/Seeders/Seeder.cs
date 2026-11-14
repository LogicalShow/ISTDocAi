using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ISTDocAi.Infrastructure.Data.Seeders.IAM;

namespace ISTDocAi.Infrastructure.Data.Seeders;

public class Seeder
{
    public async Task SeedAllAsync(IServiceProvider serviceProvider)
    {
        // IAM_User
        var iamUserSeeder = serviceProvider.GetRequiredService<UserSeeder>();
        await iamUserSeeder.SeedAsync(serviceProvider);

        //await User.SeedAsync(serviceProvider);
    }
}
