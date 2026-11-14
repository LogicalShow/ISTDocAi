using ISTDocAi.Application.Services.DocAi.AzureOperationLifecycleService;
using ISTDocAi.Application.Services.DocAi.JobLifecycleService;
using ISTDocAi.Application.Services.DocAi.JobRunLifecycleService;
using ISTDocAi.Application.Services.DocAi.WebhookDeliveryService;
using ISTDocAi.Application.Services.DocAi.CanonicalResultMapperService;
using ISTDocAi.Application.Services.DocAi.ClassifierPolicyService;
using ISTDocAi.Application.Services.DocAi.ErrorClassifierService;
using ISTDocAi.Application.Services.DocAi.Iso6346Service;
using ISTDocAi.Application.Services.DocAi.LeasePolicyService;
using ISTDocAi.Application.Services.DocAi.PageAllowanceStatusService;
using ISTDocAi.Application.Services.DocAi.ResumePolicyService;
using ISTDocAi.Application.Services.DocAi.RetryPolicyService;
using ISTDocAi.Application.Services.DocAi.StateMachinesService;
using ISTDocAi.Application.Services.DocAi.TakeoverPolicyService;
using ISTDocAi.Application.Services.DocAi.WebhookPolicyService;
using ISTDocAi.Application.Services.DocAi.ManifestAssociationPolicyService;
using ISTDocAi.Application.Services.DocAi.ManifestNumberParserService;
using ISTDocAi.Application.Services.DocAi.ManifestResultMapperService;
using ISTDocAi.Application.Services.DocAi.ManifestRowMergePolicyService;
using ISTDocAi.Application.Services.DocAi.ManifestTotalsPolicyService;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ISTDocAi.Application.Common.Behaviors;
using ISTDocAi.Application.Common.Services;
using ISTDocAi.Application.Common.IAM;
using ISTDocAi.Application.Services.Public.SettingService;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Localization;
using ISTDocAi.Application.Common.Mappings.Mapster;

namespace ISTDocAi.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = typeof(DependencyInjection).Assembly;
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddMapsterMappings(applicationAssembly);
        services.AddTransient(typeof(IPipelineBehavior<, >), typeof(ValidationBehavior<, >));
        services.AddTransient(typeof(IPipelineBehavior<, >), typeof(RequestLoggingBehavior<, >));
        services.AddScoped<IActionLogService, ActionLogService>();
        services.AddScoped<IRequestLogService, RequestLogService>();
        services.AddScoped<IErrorLogService, ErrorLogService>();
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<ICultureContext, CurrentUiCultureContext>();
        services.AddScoped<IMessageLocalizer, MessageLocalizer>();
        services.AddSingleton<AuthorizationHierarchyBuilder>();
        services.AddSingleton<IAzureOperationLifecycleService, AzureOperationLifecycleService>();
        services.AddSingleton<IJobLifecycleService, JobLifecycleService>();
        services.AddSingleton<IJobRunLifecycleService, JobRunLifecycleService>();
        services.AddSingleton<IWebhookDeliveryService, WebhookDeliveryService>();
        services.AddSingleton<ICanonicalResultMapperService, CanonicalResultMapperService>();
        services.AddSingleton<IClassifierPolicyService, ClassifierPolicyService>();
        services.AddSingleton<IErrorClassifierService, ErrorClassifierService>();
        services.AddSingleton<IIso6346Service, Iso6346Service>();
        services.AddSingleton<ILeasePolicyService, LeasePolicyService>();
        services.AddSingleton<IPageAllowanceStatusService, PageAllowanceStatusService>();
        services.AddSingleton<IResumePolicyService, ResumePolicyService>();
        services.AddSingleton<IRetryPolicyService, RetryPolicyService>();
        services.AddSingleton<IStateMachinesService, StateMachinesService>();
        services.AddSingleton<ITakeoverPolicyService, TakeoverPolicyService>();
        services.AddSingleton<IWebhookPolicyService, WebhookPolicyService>();
        services.AddSingleton<IManifestAssociationPolicyService, ManifestAssociationPolicyService>();
        services.AddSingleton<IManifestNumberParserService, ManifestNumberParserService>();
        services.AddSingleton<IManifestResultMapperService, ManifestResultMapperService>();
        services.AddSingleton<IManifestRowMergePolicyService, ManifestRowMergePolicyService>();
        services.AddSingleton<IManifestTotalsPolicyService, ManifestTotalsPolicyService>();
        return services;
    }
}
