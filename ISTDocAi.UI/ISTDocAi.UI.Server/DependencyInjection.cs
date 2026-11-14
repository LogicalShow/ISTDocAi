using ISTDocAi.Application.Startup;
using ISTDocAi.Infrastructure.Configuration;
using ISTDocAi.Infrastructure.Jobs;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration;
using Quartz;
using ISTDocAi.UI.Server.Startup;

namespace ISTDocAi.UI.Server;

public static class DependencyInjection
{
    public static IServiceCollection AddQuartzConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure ServiceJobConfiguration from settings
        services.Configure<ServiceJobConfiguration>(configuration.GetSection("ServiceJobs"));

        // Add Quartz
        services.AddQuartz(q =>
        {
            var jobConfig = configuration.GetSection("ServiceJobs").Get<ServiceJobConfiguration>();
    
            if (jobConfig?.Jobs != null)
            {
                foreach (var job in jobConfig.Jobs.Where(j => j.IsEnabled))
                {
                    var jobKey = new JobKey($"ServiceJob-{job.ServiceName}");
            
                    q.AddJob<ServiceJob>(opts => opts.WithIdentity(jobKey));

                    q.AddTrigger(opts => opts
                        .ForJob(jobKey)
                        .WithIdentity($"ServiceJobTrigger-{job.ServiceName}")
                        .StartAt(job.PlannedStartTime)
                        .WithSimpleSchedule(x => x
                            .WithInterval(TimeSpan.FromMinutes(job.TimeIntervalMinutes))
                            .RepeatForever())
                        .UsingJobData("ServiceName", job.ServiceName));
                }
            }
        });

        // Add Quartz hosted service
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }

    public static IServiceCollection AddStartupPipeline(this IServiceCollection services)
    {
        services.AddScoped<StartupPipeline>();

        services.Scan(scan => scan
            .FromAssemblyOf<DatabaseMigrationTask>()
            .AddClasses(c => c.AssignableTo<IStartupTask>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }

}