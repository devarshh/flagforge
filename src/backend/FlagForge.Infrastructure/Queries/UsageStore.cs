using System.Data;
using System.Globalization;
using System.Text;
using FlagForge.Application.Usage;
using FlagForge.Domain;
using FlagForge.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Infrastructure.Queries;

/// <summary>
/// Batched <c>MERGE</c> upserts. Each statement carries at most 300 rows (5 parameters each, under SQL Server's
/// 2,100-parameter limit); all statements share one transaction, so a failed flush can be retried without double
/// counting. Rows are sorted so concurrent pods take locks in the same order.
/// </summary>
internal sealed class UsageStore(FlagForgeDbContext db) : IUsageStore
{
    public const int MaxRowsPerStatement = 300;

    public async Task AddCountsAsync(IReadOnlyCollection<FlagUsageHourly> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return;
        }

        var ordered = rows
            .OrderBy(r => r.EnvironmentId)
            .ThenBy(r => r.FlagId)
            .ThenBy(r => r.VariationId, StringComparer.Ordinal)
            .ThenBy(r => r.HourStart)
            .ToArray();
        await db.ExecuteInTransactionAsync(
            async token =>
            {
                foreach (var chunk in ordered.Chunk(MaxRowsPerStatement))
                {
                    var (sql, parameters) = BuildMerge(chunk);
                    await db.Database.ExecuteSqlRawAsync(sql, parameters, token);
                }

                return true;
            },
            cancellationToken);
    }

    private static (string Sql, List<SqlParameter> Parameters) BuildMerge(FlagUsageHourly[] rows)
    {
        var sql = new StringBuilder("MERGE FlagUsageHourly WITH (HOLDLOCK) AS target USING (VALUES ");
        var parameters = new List<SqlParameter>(rows.Length * 5);
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            sql.Append(i == 0 ? string.Empty : ", ").Append(CultureInfo.InvariantCulture, $"(@e{i}, @f{i}, @v{i}, @h{i}, @c{i})");
            parameters.Add(new SqlParameter($"@e{i}", SqlDbType.UniqueIdentifier) { Value = row.EnvironmentId });
            parameters.Add(new SqlParameter($"@f{i}", SqlDbType.UniqueIdentifier) { Value = row.FlagId });
            parameters.Add(new SqlParameter($"@v{i}", SqlDbType.VarChar, 64) { Value = row.VariationId });
            parameters.Add(new SqlParameter($"@h{i}", SqlDbType.DateTimeOffset) { Value = row.HourStart });
            parameters.Add(new SqlParameter($"@c{i}", SqlDbType.BigInt) { Value = row.Count });
        }

        sql.Append("""
            ) AS source (EnvironmentId, FlagId, VariationId, HourStart, [Count])
            ON target.EnvironmentId = source.EnvironmentId AND target.FlagId = source.FlagId
               AND target.VariationId = source.VariationId AND target.HourStart = source.HourStart
            WHEN MATCHED THEN UPDATE SET target.[Count] = target.[Count] + source.[Count]
            WHEN NOT MATCHED THEN INSERT (EnvironmentId, FlagId, VariationId, HourStart, [Count])
                VALUES (source.EnvironmentId, source.FlagId, source.VariationId, source.HourStart, source.[Count]);
            """);
        return (sql.ToString(), parameters);
    }
}
