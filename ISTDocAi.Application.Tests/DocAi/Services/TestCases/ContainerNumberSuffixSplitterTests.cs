using ISTDocAi.Application.Services.DocAi.Iso6346Service;
using ISTDocAi.Domain.Dtos.DocAi;
using NUnit.Framework;

namespace ISTDocAi.Application.Tests.DocAi.Services;

public sealed class ContainerNumberSuffixSplitterTests
{
    [TestCase("NBCU1234564/20'", "DRY VAN", null, "NBCU1234564", "20' DRY VAN", .95)]
    [TestCase("NBCU2045673/22G1", null, null, "NBCU2045673", "22G1", .95)]
    [TestCase("KMRU5501921/45'HC", null, null, "KMRU5501921", "45'HC", .95)]
    [TestCase("CSKU8653579/40HC", "1X40HC", null, "CSKU8653579", "1X40HC", .95)]
    [TestCase("TLXU4402188/40'", "HIGH CUBE", null, "TLXU4402188", "40' HIGH CUBE", .95)]
    [TestCase("CSKU8653579/2938035", "DRY VAN", "2938035", "CSKU8653579", "DRY VAN", .95)]
    [TestCase("CSKU8653579/ML-EG12345", "DRY VAN", "999", "CSKU8653579", "DRY VAN", .5)]
    [TestCase("CSKU8653579/40HC/2938035", null, "2938035", "CSKU8653579", "40HC", .95)]
    [TestCase("NBCU1234565/20'", null, null, "NBCU1234565/20'", null, .95)]
    [TestCase("NBCU1234564", null, null, "NBCU1234564", null, .95)]
    [TestCase("nbcu1234564/20'", "DRY VAN", null, "NBCU1234564", "20' DRY VAN", .95)]
    [TestCase("CSKU8653579/40", null, null, "CSKU8653579", null, .5)]
    [TestCase("CSKU8653579/45G1/42R1/22V1/L5G1", null, null, "CSKU8653579", "45G1 42R1 22V1 L5G1", .95)]
    [TestCase("CSKU8653579/40\u2019HC", "1X40HC", null, "CSKU8653579", "1X40HC", .95)]
    [TestCase("CSKU8653579/ML-EG12345", null, "ml eg 12345", "CSKU8653579", null, .95)]
    [TestCase("CSKU8653579/20A12345", "DRY VAN", "999", "CSKU8653579", "DRY VAN", .5)]
    [TestCase("CSKU8653579/4512345", null, null, "CSKU8653579", null, .5)]
    [TestCase("CSKU8653579/40 HC", null, null, "CSKU8653579", "40 HC", .95)]
    [TestCase("CSKU8653579/40FT", "DRY", null, "CSKU8653579", "40FT DRY", .95)]
    [TestCase("CSKU8653579/40' HIGH CUBE", null, null, "CSKU8653579", "40' HIGH CUBE", .95)]
    public void Splits_only_valid_numbers(string input, string? existingType, string? seal, string expectedNumber, string? expectedType, double confidence)
    {
        var number = new ExtractedField(input, null, .95, 2);
        var type = existingType is null ? null : new ExtractedField(existingType, "printed type", .9, 1);
        var result = ContainerNumberSuffixSplitter.Split(number, new(seal, null, .9, 2), type);
        Assert.Multiple(() =>
        {
            Assert.That(result.Number.Value, Is.EqualTo(expectedNumber));
            Assert.That(result.Number.RawText, Is.EqualTo(input == expectedNumber ? null : input));
            Assert.That(result.Number.Confidence, Is.EqualTo(confidence));
            Assert.That(result.Number.Page, Is.EqualTo(2));
            Assert.That(result.Type?.Value, Is.EqualTo(expectedType));
            if (result.Type is not null)
            {
                Assert.That(result.Type.RawText, Is.EqualTo(type?.RawText));
                Assert.That(result.Type.Page, Is.EqualTo(type?.Page ?? 2));
                Assert.That(result.Type.Confidence, Is.EqualTo(type?.Confidence ?? .95));
            }
        });
    }

    [Test]
    public void Preserves_existing_raw_text_and_lower_confidence()
    {
        var result = ContainerNumberSuffixSplitter.Split(new(" CSKU 865357 9 / mystery", "original OCR", .3, 3), null, null);
        Assert.That(result.Number, Is.EqualTo(new ExtractedField("CSKU8653579", "original OCR", .3, 3)));
    }
}
