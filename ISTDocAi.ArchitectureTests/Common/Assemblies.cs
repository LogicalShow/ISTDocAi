namespace ISTDocAi.ArchitectureTests.Common;

internal static class Assemblies
{
    public static readonly Assembly Domain = typeof(ISTDocAi.Domain.Common.Shared.Response.Result).Assembly;
    public static readonly Assembly Application = typeof(ISTDocAi.Application.Common.Interfaces.ICommand).Assembly;
    public static readonly Assembly Infrastructure = typeof(ISTDocAi.Infrastructure.Data.AppDbContext).Assembly;
    public static readonly Assembly Api = typeof(ISTDocAi.UI.Server.Base.BaseController).Assembly;
}
