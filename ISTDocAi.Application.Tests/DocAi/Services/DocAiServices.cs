using Microsoft.Extensions.DependencyInjection;
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

namespace ISTDocAi.Application.Tests.DocAi.Services;
internal static class DocAiServices
{
    private static readonly ServiceProvider Provider = new ServiceCollection().AddApplication().BuildServiceProvider();
    internal static IAzureOperationLifecycleService AzureOperationLifecycleService => Provider.GetRequiredService<IAzureOperationLifecycleService>();
    internal static IJobLifecycleService JobLifecycleService => Provider.GetRequiredService<IJobLifecycleService>();
    internal static IJobRunLifecycleService JobRunLifecycleService => Provider.GetRequiredService<IJobRunLifecycleService>();
    internal static IWebhookDeliveryService WebhookDeliveryService => Provider.GetRequiredService<IWebhookDeliveryService>();
    internal static ICanonicalResultMapperService CanonicalResultMapperService => Provider.GetRequiredService<ICanonicalResultMapperService>();
    internal static IClassifierPolicyService ClassifierPolicyService => Provider.GetRequiredService<IClassifierPolicyService>();
    internal static IErrorClassifierService ErrorClassifierService => Provider.GetRequiredService<IErrorClassifierService>();
    internal static IIso6346Service Iso6346Service => Provider.GetRequiredService<IIso6346Service>();
    internal static ILeasePolicyService LeasePolicyService => Provider.GetRequiredService<ILeasePolicyService>();
    internal static IPageAllowanceStatusService PageAllowanceStatusService => Provider.GetRequiredService<IPageAllowanceStatusService>();
    internal static IResumePolicyService ResumePolicyService => Provider.GetRequiredService<IResumePolicyService>();
    internal static IRetryPolicyService RetryPolicyService => Provider.GetRequiredService<IRetryPolicyService>();
    internal static IStateMachinesService StateMachinesService => Provider.GetRequiredService<IStateMachinesService>();
    internal static ITakeoverPolicyService TakeoverPolicyService => Provider.GetRequiredService<ITakeoverPolicyService>();
    internal static IWebhookPolicyService WebhookPolicyService => Provider.GetRequiredService<IWebhookPolicyService>();
    internal static IManifestAssociationPolicyService ManifestAssociationPolicyService => Provider.GetRequiredService<IManifestAssociationPolicyService>();
    internal static IManifestNumberParserService ManifestNumberParserService => Provider.GetRequiredService<IManifestNumberParserService>();
    internal static IManifestResultMapperService ManifestResultMapperService => Provider.GetRequiredService<IManifestResultMapperService>();
    internal static IManifestRowMergePolicyService ManifestRowMergePolicyService => Provider.GetRequiredService<IManifestRowMergePolicyService>();
    internal static IManifestTotalsPolicyService ManifestTotalsPolicyService => Provider.GetRequiredService<IManifestTotalsPolicyService>();
}
