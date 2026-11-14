using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.ExcelExport;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Application.Common.Interfaces.Serialization;
using ISTDocAi.Infrastructure.BackgroundServices;
using ISTDocAi.Infrastructure.Data;
using ISTDocAi.Infrastructure.Data.Interceptors;
using ISTDocAi.Infrastructure.Data.StoredProcedures;
using ISTDocAi.Infrastructure.Data.Repositories.IAM;
using ISTDocAi.Infrastructure.Data.Seeders;
using ISTDocAi.Infrastructure.IAM.Models;
using ISTDocAi.Infrastructure.IAM.Services;
using ISTDocAi.Infrastructure.Realtime;
using ISTDocAi.Infrastructure.Services;
using ISTDocAi.Infrastructure.Services.ExcelExport;
using ISTDocAi.Infrastructure.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MediatR;
using ISTDocAi.Application.Features.IAM.Events;

namespace ISTDocAi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? connectionString, bool templateEndpointsEnabled = true)
    {
        DatabaseConnection.Validate(connectionString);

        var assembly = Assembly.GetExecutingAssembly();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDocAiSql(_ => { });
        services.AddDocAiMapping();
        services.AddOptions<Configuration.DocAi.DocAiSqlOptions>().BindConfiguration("DocAi:Sql")
            .PostConfigure<Microsoft.Extensions.Configuration.IConfiguration>((options, configuration) =>
            {
                BackgroundServices.DocAi.DatabaseConfiguration.RejectRemovedConnection(configuration);
                options.ConnectionString = connectionString!;
            });
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<StartupCancellationInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(assembly.GetName().Name)
                    .MigrationsHistoryTable("__DocAiIamMigrationsHistory", "dbo"));
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>(),
                serviceProvider.GetRequiredService<StartupCancellationInterceptor>());
        });

        services.AddIdentity<IAM_User, IAM_Role>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo<ISeeder>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped(typeof(IRepositoryOp<>), typeof(RepositoryOp<>));
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
        services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();
        services.AddSingleton<ILogPayloadSerializer, SystemTextJsonLogPayloadSerializer>();

        if (templateEndpointsEnabled)
        {
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException(
                "JWT_SECRET_KEY environment variable is required and cannot be empty.");
        if (secretKey.Length < 32)
            throw new InvalidOperationException(
                "JWT_SECRET_KEY must be at least 32 characters for security.");

        var key = Encoding.UTF8.GetBytes(secretKey);
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "JwtIssuer",
                ValidAudience = JwtAudience.Default,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };
            options.MapInboundClaims = false;

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var path = context.HttpContext.Request.Path;
                    var accessToken = context.Request.Query["access_token"];

                    // SignalR sends the token in the query string; everything else uses the
                    // Authorization header. There is no cookie fallback.
                    if (path.StartsWithSegments("/realtimeHub") && !string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = ValidateActiveUserAsync
            };
        })
        // Report tabs (/mvc/Report/...) are plain browser navigations with no Authorization
        // header, so they authenticate with a short-lived report token posted as a form field.
        .AddJwtBearer(ReportAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "JwtIssuer",
                ValidAudience = JwtAudience.Report,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };
            options.MapInboundClaims = false;

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = async context =>
                {
                    var request = context.Request;
                    if (!request.Path.StartsWithSegments(ReportAuthenticationDefaults.PathPrefix)
                        || !HttpMethods.IsPost(request.Method)
                        || !request.HasFormContentType)
                    {
                        context.NoResult();
                        return;
                    }

                    var form = await request.ReadFormAsync(context.HttpContext.RequestAborted);
                    var reportToken = form[ReportAuthenticationDefaults.FormField].FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(reportToken))
                    {
                        context.NoResult();
                        return;
                    }

                    context.Token = reportToken;
                },
                OnTokenValidated = async context =>
                {
                    var purpose = context.Principal?.FindFirst(ReportAuthenticationDefaults.PurposeClaim)?.Value;
                    if (purpose != ReportAuthenticationDefaults.PurposeValue)
                    {
                        context.Fail("Invalid report token.");
                        return;
                    }

                    await ValidateActiveUserAsync(context);
                }
            };
        });

        }
        else services.AddAuthentication();

        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("ScheduledTasksDispatcherJob");
            q.AddJob<ScheduledTasksDispatcherJob>(o => o.WithIdentity(jobKey));

            q.AddTrigger(t => t.ForJob(jobKey)
                .WithIdentity("ScheduledTasksDispatcherTrigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever()));
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskSchedulerService, TaskSchedulerService>();
        services.AddScoped<ITaskRunner, MediatRTaskRunner>();
        services.AddSingleton<IBackgroundRealtimeService, RealtimeService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<IReportTokenService, ReportTokenService>();
        services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();
        services.AddScoped<IPermissionCompressionService, PermissionCompressionService>();
        services.AddSingleton<IPermissionCacheService, PermissionCacheService>();
        services.AddTransient<INotificationHandler<UserPermissionsChangedNotification>, UserPermissionsChangedHandler>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationLogService, NotificationLogService>();
        services.AddScoped<IPasswordEncryptionService, PasswordEncryptionService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddScoped<IRealtimeService, RealtimeService>();
        services.AddScoped<IUserPasswordService, UserPasswordService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IUserPermissionService, UserPermissionService>();
        services.AddScoped<IUserMenuService, UserMenuService>();
        services.AddScoped<IUserManagementService, UserManagementService>();

        return services;
    }

    // Rejects tokens of users that were deactivated or deleted after the token was issued.
    // Backed by the permission cache, so it costs no database round trip per request.
    private static async Task ValidateActiveUserAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirst("uid")?.Value
            ?? context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Fail("Missing user identifier.");
            return;
        }

        var permissionCache = context.HttpContext.RequestServices.GetRequiredService<IPermissionCacheService>();
        var snapshot = await permissionCache.GetAsync(userId, context.HttpContext.RequestAborted);
        if (!snapshot.IsAllowed)
        {
            context.Fail("User is inactive or no longer exists.");
        }
    }
}
