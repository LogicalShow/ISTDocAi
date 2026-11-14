using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

/// <summary>Owns immutable rows; callers receive a fresh hydration and cannot mutate the cache.</summary>
public sealed class SqlMappingProfileResolver(ISqlConnectionFactory connections, IOptions<DocAiSqlOptions> options,
    TimeProvider clock, ILogger<SqlMappingProfileResolver>? logger = null) : ISqlMappingProfileResolver
{
    private readonly Func<string, Row[]>? readRows;
    // Pure cache tests supply rows at the SQL boundary; production always uses the connection factory.
    internal SqlMappingProfileResolver(TimeProvider clock, Func<string, Row[]> readRows,
        ILogger<SqlMappingProfileResolver>? logger = null)
        : this(null!, Options.Create(new DocAiSqlOptions()), clock, logger) => this.readRows = readRows;

    private sealed record Snapshot(MappingProfileHeader? Header, ImmutableArray<MappingFieldRow> Rows, DateTimeOffset Expires);
    private readonly ConcurrentDictionary<string, Snapshot> cache = new(StringComparer.Ordinal);
    private readonly object publicationGate = new();
    private readonly object[] refreshGates = Enumerable.Range(0, 32).Select(_ => new object()).ToArray();
    private const int Capacity = 1024;
    // One joined result set also keeps header and rows in the same statement snapshot.
    private const string Query = """
        SELECT b.ModelId,p.DocumentTypeCode,p.MappingShape,p.ContainersField,p.ContainerNumberField,
               p.SealNumberField,p.BillsOfLadingField,f.MapKind,f.CanonicalKey,f.AzureName
        FROM dbo.ModelMappingBindings b
        JOIN dbo.MappingProfiles p ON p.ProfileId=b.ProfileId
        LEFT JOIN dbo.MappingFields f ON f.ProfileId=p.ProfileId
        WHERE b.ModelId=@ModelId;
        """;
    internal sealed class Row
    {
        public string ExpectedDocumentTypeCode { get; set; } = "";
        public string ModelId { get; set; } = "";
        public string DocumentTypeCode { get; set; } = "";
        public string MappingShape { get; set; } = "";
        public string ContainersField { get; set; } = "";
        public string ContainerNumberField { get; set; } = "";
        public string SealNumberField { get; set; } = "";
        public string BillsOfLadingField { get; set; } = "";
        public string? MapKind { get; set; }
        public string? CanonicalKey { get; set; }
        public string? AzureName { get; set; }
    }

    public AnalyzeResultMappingOptions Resolve(string modelId, string documentType, bool allowLegacyDocumentType = false)
    {
        // Warm reads never wait for another model's database refresh.
        if (!cache.TryGetValue(modelId, out var snapshot) || clock.GetUtcNow() >= snapshot.Expires)
        {
            // Fixed stripes bound gate storage and coalesce refreshes for the same producing model.
            lock (refreshGates[StringComparer.Ordinal.GetHashCode(modelId) & 31])
            {
                if (!cache.TryGetValue(modelId, out snapshot) || clock.GetUtcNow() >= snapshot.Expires)
                {
                    try
                    {
                        var loaded = Load(modelId);
                        if (loaded.Header is null && snapshot?.Header is not null)
                            throw MappingProfiles.ConfigurationError();
                        snapshot = loaded;
                    }
                    catch when (snapshot?.Header is not null)
                    {
                        snapshot = snapshot with { Expires = clock.GetUtcNow().AddSeconds(30) };
                        // No exception object or source values may enter diagnostics.
                        try { logger?.LogWarning("{event} modelId={modelId}", "mapping.refresh_failed", modelId); }
                        catch { /* Diagnostics cannot invalidate a usable snapshot. */ }
                    }
                    lock (publicationGate)
                    {
                        if (cache.Count >= Capacity && !cache.ContainsKey(modelId))
                            cache.TryRemove(cache.MinBy(p => p.Value.Expires).Key, out _);
                        cache[modelId] = snapshot;
                    }
                }
            }
        }
        if (snapshot.Header is null || snapshot.Header.DocumentTypeCode != documentType)
            throw MappingProfiles.ConfigurationError();
        return MappingProfileHydrator.Hydrate(snapshot.Header, snapshot.Rows);
    }

    private Snapshot Load(string modelId)
    {
        var rows = readRows is null ? ReadSqlRows(modelId) : readRows(modelId);
        if (rows.Length == 0) return new(null, [], clock.GetUtcNow().AddSeconds(5));
        var (header, fields) = ReadProfile(rows);
        return new(header, fields, clock.GetUtcNow().AddSeconds(60));
    }

    private Row[] ReadSqlRows(string modelId)
    {
        using var connection = connections.Open();
        return connection.Query<Row>(Query, new { ModelId = modelId },
            commandTimeout: options.Value.CommandTimeoutSeconds).ToArray();
    }

    internal static (MappingProfileHeader Header, ImmutableArray<MappingFieldRow> Fields) ReadProfile(Row[] rows)
    {
        var first = rows[0];
        var header = new MappingProfileHeader(first.ModelId, first.DocumentTypeCode, Enum.Parse<MappingShape>(first.MappingShape),
            first.ContainersField, first.ContainerNumberField, first.SealNumberField, first.BillsOfLadingField);
        var fields = rows.Where(r => r.MapKind is not null).Select(r => new MappingFieldRow(
            Enum.Parse<MappingMapKind>(r.MapKind!), r.CanonicalKey!, r.AzureName!)).ToImmutableArray();
        // Reject malformed snapshots before publishing anything.
        _ = MappingProfileHydrator.Hydrate(header, fields);
        return (header, fields);
    }
}

