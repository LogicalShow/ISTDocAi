using ISTDocAi.Application.Services.DocAi.Pricing;
using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.Pricing;
public sealed class AzurePricePolicyTests
{
    internal static readonly DateTimeOffset Now = new(2026,11,16,23,59,59,TimeSpan.Zero);
    internal static AzurePriceObservation Row(bool extract = true) => new(extract ? AzurePricePolicy.ExtractId : AzurePricePolicy.ClassifyId,
        extract ? "S0 Custom Pages" : "S0 pages for doc classifier", "USD", "francecentral", "1K", "Consumption", 0,
        extract ? 30 : 3, new DateTimeOffset(2024,6,1,0,0,0,TimeSpan.Zero), "Azure Document Intelligence", "S0", "Foundry Tools");
    [TestCase("id")][TestCase("name")][TestCase("currency")][TestCase("region")][TestCase("unit")][TestCase("type")]
    [TestCase("tier")][TestCase("product")][TestCase("sku")][TestCase("service")][TestCase("nullDate")]
    [TestCase("nullPrice")][TestCase("zero")][TestCase("negative")][TestCase("precision")][TestCase("range")][TestCase("future")]
    public void Invalid_meter_refuses_the_pair(string field)
    {
        var r = Row();
        r = field switch {
            "id" => r with { MeterId="wrong" }, "name" => r with { MeterName=null }, "currency" => r with { Currency="EUR" },
            "region" => r with { Region=null }, "unit" => r with { Unit="1" }, "type" => r with { Type="Reservation" },
            "tier" => r with { TierMinimumUnits=null }, "product" => r with { ProductName="Azure Document Intelligence - Disconnected" },
            "sku" => r with { SkuName="S1" }, "service" => r with { ServiceName=null }, "nullDate" => r with { AzureEffectiveFrom=null },
            "nullPrice" => r with { Price=null }, "zero" => r with { Price=0 }, "negative" => r with { Price=-1 },
            "precision" => r with { Price=30.00001m }, "range" => r with { Price=100000 }, "future" => r with { AzureEffectiveFrom=Now.AddSeconds(1) },
            _ => throw new ArgumentException(field) };
        Assert.That(AzurePricePolicy.Evaluate([r,Row(false)],Now).Any(x=>x.ReasonCode is not null), Is.True);
    }
    [TestCase("S0 Custom Generative Pages")][TestCase("S0 Batch Custom Extraction Pages")][TestCase("S0 Batch Document Classifier Pages")]
    public void Lookalikes_are_never_accepted(string name) => Assert.That(AzurePricePolicy.Evaluate([Row() with { MeterName=name },Row(false)],Now)[0].ReasonCode, Is.EqualTo("INVALID_METER"));
    [Test]
    public void Missing_and_ambiguous_are_not_deduplicated()
    {
        Assert.That(AzurePricePolicy.Evaluate([],Now).All(x=>x.ReasonCode=="METER_MISSING"), Is.True);
        Assert.That(AzurePricePolicy.Evaluate([Row(),Row(),Row(false)],Now)[0].MatchedRowCount, Is.EqualTo(2));
    }
    [Test]
    public void Any_representable_positive_price_including_large_changes_is_accepted()
    {
        var result = AzurePricePolicy.Evaluate([Row() with { Price=99999.9999m },Row(false) with { Price=.0001m }],Now);
        Assert.That(result.All(x=>x.ReasonCode is null), Is.True);
        Assert.That(result[0].Source, Does.EndWith("read=2026-11-16T23:59:59Z"));
        Assert.That(result.All(x=>x.Source!.Length<=200), Is.True);
    }
}
