using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Analysis;
using ISTDocAi.Infrastructure.Services.DocAi.Storage;
using ISTDocAi.Infrastructure.Services.DocAi.Identity;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Services.DocAi.Identity;

public static class IdentityCredentialFactory
{
    public static TokenCredential Create(IdentityOptions options, bool development, string? managedClientId, CertificateIdentity identity) =>
        development && !UsesCertificate(options, development) ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedClientId }) :
        options.Mode == "ClientCertificate" ? new ClientCertificateCredential(options.TenantId, options.ClientId, identity.Certificate!) :
        new ManagedIdentityCredential(string.IsNullOrWhiteSpace(managedClientId) ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(managedClientId));
    public static bool UsesCertificate(IdentityOptions options, bool development) =>
        options.Mode == "ClientCertificate" && (!development || IdentityOptions.IsValid(options));
}
