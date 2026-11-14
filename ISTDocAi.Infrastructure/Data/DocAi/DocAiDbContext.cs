using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.Infrastructure.Data.DocAi;

/// <summary>Schema migrations only. Runtime persistence remains Dapper.</summary>
public sealed partial class DocAiDbContext(DbContextOptions<DocAiDbContext> options) : DbContext(options);
