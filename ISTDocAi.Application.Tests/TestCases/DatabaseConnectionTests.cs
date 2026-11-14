using ISTDocAi.Infrastructure.Data;

namespace ISTDocAi.Application.Tests.TestCases;

[TestFixture]
public class DatabaseConnectionTests
{
    [TestCase("master")]
    [TestCase("model")]
    [TestCase("msdb")]
    [TestCase("tempdb")]
    [TestCase("MASTER")]
    public void SystemDatabase_ShouldBeRejected(string database)
    {
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnection.Validate($"Server=.;Initial Catalog={database};Integrated Security=True"));
    }

    [Test]
    public void MissingDatabase_ShouldBeRejected()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseConnection.Validate("Server=.;Integrated Security=True"));
    }

    [Test]
    public void NamedApplicationDatabase_ShouldBeAccepted()
    {
        Assert.DoesNotThrow(() => DatabaseConnection.Validate("Server=.;Database=Example;Integrated Security=True"));
    }
}
