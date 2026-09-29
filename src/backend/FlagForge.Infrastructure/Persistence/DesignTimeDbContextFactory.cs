using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlagForge.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> tooling. Adding migrations never connects, so a placeholder connection string is
/// fine; set <c>ConnectionStrings__Sql</c> to point other commands at a real server.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FlagForgeDbContext>
{
    public FlagForgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = System.Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? "Server=localhost,1433;Database=flagforge;TrustServerCertificate=True";
        var builder = new DbContextOptionsBuilder<FlagForgeDbContext>();
        FlagForgeDbContext.Configure(builder, connectionString);
        return new FlagForgeDbContext(builder.Options);
    }
}
