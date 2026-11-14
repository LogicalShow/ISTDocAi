using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.Processing;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Configuration.DocAi;
public sealed class WorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxParallelJobs { get; set; } = 4;
}
