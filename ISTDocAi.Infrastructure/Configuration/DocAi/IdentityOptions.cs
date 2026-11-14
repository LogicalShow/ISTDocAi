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
namespace ISTDocAi.Infrastructure.Configuration.DocAi;

public sealed class IdentityOptions
{
    public string Mode { get; set; } = "ManagedIdentity";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string CertificateThumbprint { get; set; } = "";
    public string StoreLocation { get; set; } = "LocalMachine";
    public static bool IsValid(IdentityOptions o) =>
        o.Mode is "ManagedIdentity" or "ClientCertificate" &&
        o.StoreLocation is "LocalMachine" or "CurrentUser" &&
        (o.Mode != "ClientCertificate" || (Guid.TryParse(o.TenantId, out _) && Guid.TryParse(o.ClientId, out _) &&
        o.CertificateThumbprint is { Length: 40 } && o.CertificateThumbprint.All(Uri.IsHexDigit)));
}
