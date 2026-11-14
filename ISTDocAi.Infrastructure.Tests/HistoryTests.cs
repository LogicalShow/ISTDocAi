using NUnit.Framework;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class HistoryTests
{
    private static readonly string[] Known = ["20261101000000_InitialSchema", "20261102000000_Next"];

    [Test]
    public void DifferentEfVersionsAreAcceptedInKnownPrefix()
    {
        Assert.That(DatabaseInitializer.ValidateHistoryRows([(Known[0], "10.0.13"), (Known[1], "10.0.14")], Known), Is.True);
        Assert.That(DatabaseInitializer.ValidateHistoryRows([(Known[0], "10.0.13")], Known), Is.True);
        Assert.That(DatabaseInitializer.ValidateHistoryRows([], Known), Is.False);
    }

        [TestCase("99999999999999_Unknown", "10.0.14")]
    [TestCase("20261102000000_Next", "")]
    [TestCase("20261102000000_Next", " ")]
    public void UnknownIdsAndEmptyVersionsAreRefused(string id, string version)
    {
        var error = Assert.Throws<DatabaseInitializationException>(() =>
            DatabaseInitializer.ValidateHistoryRows([(Known[0], "10.0.13"), (id, version)], Known));
        Assert.That(error!.Code, Is.EqualTo("DATABASE_HISTORY_UNRECOGNIZED"));
    }

    [Test]
    public void GapsAndReorderedRowsAreRefused() => Assert.Throws<DatabaseInitializationException>(() =>
        DatabaseInitializer.ValidateHistoryRows([(Known[1], "10.0.14")], Known));
}
