using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using Microsoft.Extensions.Options;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class MappingFingerprintTests
{
    [Test]
    public void Fingerprint_is_content_based_ordinal_and_independent_of_unrelated_profiles()
    {
        var first = new AnalyzeResultMappingOptions { FieldMap = new() { ["z"] = "Z", ["a"] = "A" } };
        var second = new AnalyzeResultMappingOptions { FieldMap = new() { ["a"] = "A", ["z"] = "Z" } };
        var hash = InMemoryMappingProfileResolver.Fingerprint(first, "v1", "BL");
        Assert.Matches("^[0-9a-f]{64}$", hash);
        // Frozen canonical digest: checked against an independent implementation, persists across process restarts.
        Assert.Equal("f24e3448beb555647ba7c87b9f1e26417b9ad994176a87ac1aad8b0825131bbc", hash);
        Assert.Equal(hash, InMemoryMappingProfileResolver.Fingerprint(second, "v1", "BL"));
        second.Models["unrelated"] = new() { DocumentTypeCode = "MANIFEST" };
        Assert.Equal(hash, InMemoryMappingProfileResolver.Fingerprint(second, "v2", "BL"));
        second.FieldMap["a"] = "changed";
        Assert.NotEqual(hash, InMemoryMappingProfileResolver.Fingerprint(second, "v1", "BL"));
        Assert.Equal("2", MappingFingerprint.MappingAlgorithmVersion);
        Assert.NotEqual(hash, InMemoryMappingProfileResolver.Fingerprint(first, "v1", "BL", "1"));
    }

    [Test]
    public void Selected_profile_ignores_legacy_settings_and_covers_manifest_maps()
    {
        var profile = new AnalyzeResultMappingProfile { DocumentTypeCode = "MANIFEST" };
        var options = new AnalyzeResultMappingOptions { Models = new() { ["m"] = profile } };
        var hash = InMemoryMappingProfileResolver.Fingerprint(options, "m", "MANIFEST");
        options.FieldMap.Clear();
        Assert.Equal(hash, InMemoryMappingProfileResolver.Fingerprint(options, "m", "MANIFEST"));
        profile.BillFieldMap["billOfLadingNumber"] = "BL";
        Assert.NotEqual(hash, InMemoryMappingProfileResolver.Fingerprint(options, "m", "MANIFEST"));
    }

    [Test]
    public void Actual_parser_returns_the_fingerprint_of_the_profile_used()
    {
        var options = new AnalyzeResultMappingOptions();
        var parser = new AnalysisParser(new InMemoryMappingProfileResolver(Options.Create(options)));
        var result = parser.Map("""{"pages":[{}],"documents":[{"fields":{}}]}""", "BL", null, "v1", .8);
        Assert.Equal(InMemoryMappingProfileResolver.Fingerprint(options, "v1", "BL"), result.MappingFingerprint);
        Assert.Equal(MappingFingerprint.CanonicalContractVersion, result.Result.ContractVersion);
    }
}
