using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class PageAllowanceStatusTests
{
    [TestCase("Prepaid", 13, 120L, 10, "Ok", 12L)]
    [TestCase("Prepaid", 12, 120L, 10, "Low", 12L)]
    [TestCase("Prepaid", 1, 5L, 10, "Low", 1L)]
    [TestCase("Prepaid", 0, 5L, 10, "Exhausted", 1L)]
    [TestCase("Prepaid", -1, null, 10, "Exhausted", null)]
    [TestCase("Prepaid", 2, null, 10, "Ok", null)]
    [TestCase("Unlimited", -1, 5L, 10, "Unlimited", 1L)]
    [TestCase("Unlimited", -30, -25L, 10, "Unlimited", -3L)]
    [TestCase("Prepaid", 1, 0L, 10, "Ok", 0L)]
    [TestCase("Prepaid", 1, long.MaxValue, 50, "Low", 4611686018427387903L)]
    public void Calculates_status_and_floor_without_overflow(string mode, long remaining, long? cycle, int percent, string status, long? warning)
    {
        var result = DocAiServices.PageAllowanceStatusService.Calculate(mode, remaining, cycle, percent);
        Assert.That(result.Status, Is.EqualTo(status));
        Assert.That(result.WarningAtPages, Is.EqualTo(warning));
    }
}
