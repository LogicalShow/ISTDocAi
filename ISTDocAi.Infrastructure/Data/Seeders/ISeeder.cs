namespace ISTDocAi.Infrastructure.Data.Seeders;

public interface ISeeder
{
    int Order { get; }
    Task SeedAsync(IServiceProvider serviceProvider);
}
