using NUnit.Framework;
using Assert = ISTDocAi.UI.Server.Tests.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Infrastructure;

using ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
using ISTDocAi.Infrastructure.Tests.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Reflection;
using System.Text.RegularExpressions;
namespace ISTDocAi.UI.Server.Tests.DocAi;
public sealed class TelemetryTests
{
    [Test]
    public void Infrastructure_and_app_settings_contain_no_key_lookup_or_secret_connection_string()
    {
        var root = RepositoryPaths.Root;
        var paths = new[] { "ISTDocAi.Application", "ISTDocAi.Infrastructure", "ISTDocAi.UI/ISTDocAi.UI.Server" }
            .SelectMany(folder => Directory.GetFiles(Path.Combine(root, folder), "appsettings*.json", SearchOption.AllDirectories))
            .Where(path => !path.Contains("/bin/") && !path.Contains("/obj/") && !path.Contains("\\bin\\") && !path.Contains("\\obj\\"));
        var forbidden = File.ReadAllLines(Path.Combine(root, ".github", "scripts", "config-forbidden.txt"));
        foreach (var path in paths)
            foreach (var token in forbidden)
                Assert.False(File.ReadAllText(path).Contains(token, StringComparison.OrdinalIgnoreCase), "Forbidden secret pattern in configuration.");
    }
}
