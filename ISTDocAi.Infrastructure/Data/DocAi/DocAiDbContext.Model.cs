using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace ISTDocAi.Infrastructure.Data.DocAi;

public sealed partial class DocAiDbContext
{
    public DbSet<Consumer> Consumers { get; set; } = null!;
    public DbSet<ModelRegistry> ModelRegistry { get; set; } = null!;
    public DbSet<ClassifierClass> ClassifierClasses { get; set; } = null!;
    public DbSet<Job> Jobs { get; set; } = null!;
    public DbSet<JobRun> JobRuns { get; set; } = null!;
    public DbSet<JobSubmission> JobSubmissions { get; set; } = null!;
    public DbSet<AzureOperation> AzureOperations { get; set; } = null!;
    public DbSet<JobResult> JobResults { get; set; } = null!;
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; } = null!;
    public DbSet<AzurePriceCheck> AzurePriceChecks { get; set; } = null!;
    public DbSet<AzureMeterPrice> AzureMeterPrices { get; set; } = null!;
    public DbSet<UsageLedger> UsageLedger { get; set; } = null!;
    public DbSet<BillingPeriod> BillingPeriods { get; set; } = null!;
    public DbSet<MonthlyConsumerCost> MonthlyConsumerCosts { get; set; } = null!;
    public DbSet<PageAllowanceGrant> PageAllowanceGrants { get; set; } = null!;
    public DbSet<PageReservation> PageReservations { get; set; } = null!;
    public DbSet<CustomerPageCharge> CustomerPageCharges { get; set; } = null!;
    public DbSet<PageQuotaOperatorRequest> PageQuotaOperatorRequests { get; set; } = null!;
    public DbSet<MappingProfile> MappingProfiles { get; set; } = null!;
    public DbSet<MappingField> MappingFields { get; set; } = null!;
    public DbSet<ModelMappingBinding> ModelMappingBindings { get; set; } = null!;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(DocAiDbContext).Assembly,
            type => type.Namespace == "ISTDocAi.Infrastructure.Data.Configurations.DocAi");
    }
}
