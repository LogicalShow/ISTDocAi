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
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace ISTDocAi.Infrastructure.Services.DocAi.Secrets;

internal static class PrivateCa
{
    public static X509Certificate2 Parse(string pem)
    {
        // Exactly one public CA certificate; refuse other blocks, private keys and trailing data.
        var remaining = pem.AsSpan().Trim();
        if (!PemEncoding.TryFind(remaining, out var fields) || fields.Location.Start.GetOffset(remaining.Length) != 0 ||
            !remaining[fields.Label].SequenceEqual("CERTIFICATE") ||
            !remaining[fields.Location.End.GetOffset(remaining.Length)..].Trim().IsEmpty)
            throw new CryptographicException("Invalid public CA PEM.");
        var certificate = X509Certificate2.CreateFromPem(pem);
        if (!certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority))
        { certificate.Dispose(); throw new CryptographicException("CA certificate required."); }
        return certificate;
    }
}
