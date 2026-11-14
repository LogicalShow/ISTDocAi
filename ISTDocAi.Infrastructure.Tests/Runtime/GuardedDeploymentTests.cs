using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
public abstract class GuardedDeploymentTests
{
    [SetUp]
    public void RequireOptIn()
    {
        var categories = TestContext.CurrentContext.Test.Properties["Category"].Cast<string>()
            .Concat(GetType().GetCustomAttributes(typeof(CategoryAttribute), true).Cast<CategoryAttribute>().Select(x => x.Name));
        if (categories.Contains("Sql")) SqlTestGuard.Require();
        if (categories.Contains("Machine")) MachineGate.Require();
    }
}
