using ISTDocAi.Domain.Common.Constants.DocAi;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Application.Services.DocAi.ManifestNumberParserService;

namespace ISTDocAi.Application.Services.DocAi.ManifestTotalsPolicyService;
public sealed class ManifestTotalsPolicyService(IManifestNumberParserService _manifestNumberParserService) : IManifestTotalsPolicyService
{
    public ImmutableArray<FieldFlag> Check(ImmutableDictionary<string, ExtractedField> fields, ImmutableArray<ManifestContainer> containers, string path)
    {
        var flags = ImmutableArray.CreateBuilder<FieldFlag>();
        var unverified = false;
        foreach (var name in new[]
        {
            "containerCount",
            "packages",
            "grossWeight"
        }

        )
        {
            if (!fields.TryGetValue(name, out var total))
                continue;
            if (!_manifestNumberParserService.TryParse(total.Value, out var expected) || (name == "containerCount" && expected != decimal.Truncate(expected)) || (containers.Length == 0 && !fields.ContainsKey("containerCount")))
            {
                unverified = true;
                continue;
            }

            decimal actual = containers.Length;
            if (name != "containerCount")
            {
                actual = 0;
                var valid = true;
                foreach (var container in containers)
                {
                    if (!container.Fields.TryGetValue(name, out var field) || !_manifestNumberParserService.TryParse(field.Value, out var number) || number > decimal.MaxValue - actual)
                    {
                        valid = false;
                        break;
                    }

                    actual += number;
                }

                if (!valid)
                {
                    unverified = true;
                    continue;
                }
            }

            var tolerance = name == "grossWeight" ? Math.Max(1m, expected * .005m) : 0m;
            if (Math.Abs(expected - actual) > tolerance)
                flags.Add(new(path + ".fields." + name, FlagReasons.TotalsMismatch));
        }

        if (flags.Count > 0)
            flags.Add(new(path, FlagReasons.TotalsMismatch));
        if (unverified)
            flags.Add(new(path, FlagReasons.TotalsUnverified));
        return flags.ToImmutable();
    }
}
