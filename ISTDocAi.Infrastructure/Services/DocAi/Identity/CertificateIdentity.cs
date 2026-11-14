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

public sealed class CertificateIdentity : IDisposable
{
    public X509Certificate2? Certificate { get; }
    private readonly TimeProvider clock;
    public CertificateIdentity(IdentityOptions options, TimeProvider clock, bool development, Func<IdentityOptions, X509Certificate2>? load = null)
    {
        this.clock = clock;
        if (!IdentityCredentialFactory.UsesCertificate(options, development)) return;
        if (load is not null) Certificate = load(options);
        else
        {
            try
            {
                using var store = new X509Store(StoreName.My, Enum.Parse<StoreLocation>(options.StoreLocation));
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var matches = store.Certificates.Find(X509FindType.FindByThumbprint, options.CertificateThumbprint, false);
                try
                {
                    var cert = matches.OfType<X509Certificate2>().FirstOrDefault(c => c.HasPrivateKey);
                    if (cert is null) throw new InvalidOperationException();
                    Certificate = new X509Certificate2(cert);
                }
                finally { foreach (var cert in matches) cert.Dispose(); }
            }
            catch (Exception) { throw new InvalidOperationException("DocAi identity certificate was not found with an accessible private key in the configured My store."); }
        }
        if (!IsReady) { Dispose(); throw new InvalidOperationException("DocAi identity certificate is outside its validity dates."); }
        try
        {
            using var key = Certificate.GetRSAPrivateKey();
            if (key is null) throw new InvalidOperationException();
            _ = key.SignData([1], System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        }
        catch (Exception) { Dispose(); throw new InvalidOperationException("DocAi identity certificate private key is inaccessible or is not RSA."); }
    }
    public bool IsReady => Certificate is null || (clock.GetUtcNow() >= Certificate.NotBefore.ToUniversalTime() && clock.GetUtcNow() < Certificate.NotAfter.ToUniversalTime());
    public double? DaysLeft => Certificate is null ? null : (Certificate.NotAfter.ToUniversalTime() - clock.GetUtcNow()).TotalDays;
    public void Dispose() => Certificate?.Dispose();
}
