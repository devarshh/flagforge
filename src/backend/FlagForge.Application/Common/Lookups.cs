using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Common;

/// <summary>Tracked lookups by key that throw <see cref="NotFoundException"/> with a readable message.</summary>
public static class Lookups
{
    public static async Task<Project> GetProjectAsync(this IFlagForgeDbContext db, string projectKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        return await db.Projects.FirstOrDefaultAsync(p => p.Key == projectKey, cancellationToken)
            ?? throw new NotFoundException($"Project '{projectKey}' does not exist.");
    }

    public static async Task<ProjectEnvironment> GetEnvironmentAsync(
        this IFlagForgeDbContext db, Project project, string environmentKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(project);
        return await db.Environments.FirstOrDefaultAsync(e => e.ProjectId == project.Id && e.Key == environmentKey, cancellationToken)
            ?? throw new NotFoundException($"Environment '{environmentKey}' does not exist in project '{project.Key}'.");
    }

    public static async Task<Flag> GetFlagAsync(this IFlagForgeDbContext db, Project project, string flagKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(project);
        return await db.Flags.FirstOrDefaultAsync(f => f.ProjectId == project.Id && f.Key == flagKey, cancellationToken)
            ?? throw new NotFoundException($"Flag '{flagKey}' does not exist in project '{project.Key}'.");
    }
}
