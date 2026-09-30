using FlagForge.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Respawn;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace FlagForge.Testing;

/// <summary>
/// One SQL Server and one Redis container per test assembly (an xUnit assembly fixture). Migrations run once;
/// <see cref="ResetAsync"/> clears data between tests with Respawn. App hosts are created lazily and share one
/// <see cref="FakeTimeProvider"/>, so tests control time for every service at once.
/// </summary>
public sealed class FlagForgeFixture : IAsyncLifetime
{
    public const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";
    public const string RedisImage = "redis:7-alpine";

    private readonly MsSqlContainer _sql = new MsSqlBuilder(SqlServerImage).Build();
    private readonly RedisContainer _redis = new RedisBuilder(RedisImage).Build();
    private readonly List<IAsyncDisposable> _hosts = [];
    private Respawner? _respawner;
    private ManagementApiFactory? _managementApi;
    private EvaluationApiFactory? _evaluationApi;

    /// <summary>Starts at the real current time and only moves forward.</summary>
    public FakeTimeProvider Time { get; } = new(TimeProvider.System.GetUtcNow());

    public string SqlConnectionString { get; private set; } = string.Empty;

    public string RedisConnectionString => _redis.GetConnectionString();

    public ManagementApiFactory ManagementApi => _managementApi ??= Track(new ManagementApiFactory(this));

    public EvaluationApiFactory EvaluationApi => _evaluationApi ??= Track(new EvaluationApiFactory(this));

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _redis.StartAsync());
        SqlConnectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "flagforge_tests" }.ConnectionString;

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(SqlConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = ["__EFMigrationsHistory"],
            WithReseed = true,
        });
    }

    /// <summary>
    /// Deletes all rows (except migration history) so each test starts from an empty database, and drops the
    /// evaluation API's in-memory state (snapshots, cached keys, buffered usage).
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(SqlConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
        _evaluationApi?.ResetState();
    }

    /// <summary>A DbContext for arranging and asserting on data directly.</summary>
    public FlagForgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FlagForgeDbContext>();
        FlagForgeDbContext.Configure(options, SqlConnectionString);
        return new FlagForgeDbContext(options.Options);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        await _sql.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private T Track<T>(T host)
        where T : IAsyncDisposable
    {
        _hosts.Add(host);
        return host;
    }
}
