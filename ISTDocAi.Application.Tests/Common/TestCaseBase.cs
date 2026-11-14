namespace ISTDocAi.Application.Tests.Common;

public abstract class TestCaseBase
{
    protected IServiceScope CreateServiceProviderScope(Action<IServiceCollection>? configure = null)
    {
        // create service provider scope
        return ServiceProviderFactory.CreateServiceProviderScope(configure);
    }
}
