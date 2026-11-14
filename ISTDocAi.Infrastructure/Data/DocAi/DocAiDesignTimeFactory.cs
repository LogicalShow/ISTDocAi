using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ISTDocAi.Infrastructure.Data.DocAi;

public sealed class DocAiDesignTimeFactory : IDesignTimeDbContextFactory<DocAiDbContext>
{
    public DocAiDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<DocAiDbContext>()
        .UseSqlServer("Server=127.0.0.1;Database=DocAiDesignTime;Integrated Security=true;Connect Timeout=1;Encrypt=true",
            sql => sql.CommandTimeout(300)).Options);
}
