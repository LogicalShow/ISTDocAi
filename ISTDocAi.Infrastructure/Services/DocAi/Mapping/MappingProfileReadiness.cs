using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

/// <summary>One statement snapshot of all active extract bindings, cached for both success and failure.</summary>
public sealed class MappingProfileReadiness(ISqlConnectionFactory connections, IOptions<DocAiSqlOptions> sqlOptions,
    TimeProvider clock, ILogger<MappingProfileReadiness> logger) : IDisposable
{
    // Offline host tests supply the SQL row boundary; production always executes QueryAsync.
    private readonly Func<CancellationToken, Task<IEnumerable<SqlMappingProfileResolver.Row>>>? readRows;
    internal MappingProfileReadiness(ISqlConnectionFactory connections, IOptions<DocAiSqlOptions> sqlOptions,
        TimeProvider clock, ILogger<MappingProfileReadiness> logger,
        Func<CancellationToken, Task<IEnumerable<SqlMappingProfileResolver.Row>>> readRows)
        : this(connections, sqlOptions, clock, logger) => this.readRows = readRows;

    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset expires;
    private bool ready;
    private const string Query = """
        SELECT m.ModelId,m.DocumentTypeCode AS ExpectedDocumentTypeCode,
               p.DocumentTypeCode,p.MappingShape,p.ContainersField,p.ContainerNumberField,
               p.SealNumberField,p.BillsOfLadingField,f.MapKind,f.CanonicalKey,f.AzureName
        FROM dbo.ModelRegistry m
        LEFT JOIN dbo.ModelMappingBindings b ON b.ModelId=m.ModelId
        LEFT JOIN dbo.MappingProfiles p ON p.ProfileId=b.ProfileId
        LEFT JOIN dbo.MappingFields f ON f.ProfileId=p.ProfileId
        WHERE m.Kind='extract' AND m.Status='Active';
        """;

    public async Task<bool> CheckAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (clock.GetUtcNow() < expires) return ready;
            // Publish a negative cache budget even if this probe is cancelled after starting SQL.
            ready = false;
            expires = clock.GetUtcNow().AddSeconds(60);
            try
            {
                await using var connection = await connections.OpenAsync(ct);
                var rows = readRows is not null ? await readRows(ct) : await connection.QueryAsync<SqlMappingProfileResolver.Row>(new CommandDefinition(Query,
                    commandTimeout: sqlOptions.Value.CommandTimeoutSeconds, cancellationToken: ct));
                var missing = new List<string>();
                foreach (var model in rows.GroupBy(r => r.ModelId, StringComparer.Ordinal))
                {
                    try
                    {
                        var (header, _) = SqlMappingProfileResolver.ReadProfile(model.ToArray());
                        if (header.DocumentTypeCode != model.First().ExpectedDocumentTypeCode) missing.Add(model.Key);
                    }
                    catch (Exception) { missing.Add(model.Key); }
                }
                ready = missing.Count == 0;
                if (!ready)
                {
                    try { logger.LogWarning("{event} modelIds={modelIds}", "mapping.active_profile_missing",
                        string.Join(",", missing.Order(StringComparer.Ordinal))); }
                    catch { /* Logging cannot change readiness. */ }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) { ready = false; }
            expires = clock.GetUtcNow().AddSeconds(60);
            return ready;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
