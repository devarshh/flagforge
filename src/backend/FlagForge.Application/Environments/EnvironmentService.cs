using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Environments;

public sealed class EnvironmentService(
    IFlagForgeDbContext db,
    IAuditWriter audit,
    IChangeNotifier notifier,
    TimeProvider timeProvider,
    IValidator<CreateEnvironmentRequest> createValidator,
    IValidator<UpdateEnvironmentRequest> updateValidator)
{
    public async Task<IReadOnlyList<EnvironmentResponse>> ListAsync(string projectKey, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var environments = await db.Environments.AsNoTracking()
            .Where(e => e.ProjectId == project.Id)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Key)
            .ToListAsync(cancellationToken);
        return [.. environments.Select(EnvironmentResponse.From)];
    }

    public async Task<EnvironmentResponse> GetAsync(string projectKey, string environmentKey, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        return EnvironmentResponse.From(await db.GetEnvironmentAsync(project, environmentKey, cancellationToken));
    }

    /// <summary>Creates the environment and a default (disabled) config for every flag in the project.</summary>
    public async Task<EnvironmentResponse> CreateAsync(
        string projectKey, CreateEnvironmentRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        return await db.ExecuteInTransactionAsync(
            async token =>
            {
                var project = await db.GetProjectAsync(projectKey, token);
                if (await db.Environments.AnyAsync(e => e.ProjectId == project.Id && e.Key == request.Key, token))
                {
                    throw new ConflictException($"Project '{project.Key}' already has an environment with the key '{request.Key}'.");
                }

                var now = timeProvider.GetUtcNow();
                var nextSortOrder = await db.Environments.Where(e => e.ProjectId == project.Id).MaxAsync(e => (int?)e.SortOrder, token) ?? -1;
                var environment = new ProjectEnvironment
                {
                    Id = Guid.CreateVersion7(now),
                    ProjectId = project.Id,
                    Key = request.Key,
                    Name = request.Name.Trim(),
                    Color = request.Color.ToUpperInvariant(),
                    IsProtected = request.IsProtected,
                    SortOrder = nextSortOrder + 1,
                    CreatedAt = now,
                };
                db.Environments.Add(environment);

                var flags = await db.Flags.Where(f => f.ProjectId == project.Id).ToListAsync(token);
                db.FlagEnvironmentConfigs.AddRange(flags.Select(flag =>
                    FlagEnvironmentConfig.CreateDefault(Guid.CreateVersion7(now), flag, environment.Id, now)));

                audit.Record(actor, AuditActions.EnvironmentCreated, AuditTarget.For(project, environment), after: Snapshot(environment));
                await db.SaveChangesAsync(token);
                return EnvironmentResponse.From(environment);
            },
            cancellationToken);
    }

    public async Task<EnvironmentResponse> UpdateAsync(
        string projectKey, string environmentKey, UpdateEnvironmentRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken);
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var before = Snapshot(environment);
        environment.Name = request.Name?.Trim() ?? environment.Name;
        environment.Color = request.Color?.ToUpperInvariant() ?? environment.Color;
        environment.IsProtected = request.IsProtected ?? environment.IsProtected;
        environment.SortOrder = request.SortOrder ?? environment.SortOrder;
        audit.Record(actor, AuditActions.EnvironmentUpdated, AuditTarget.For(project, environment), before: before, after: Snapshot(environment));
        await db.SaveChangesAsync(cancellationToken);
        return EnvironmentResponse.From(environment);
    }

    /// <summary>Deletes the environment with its configs, schedules, usage, and SDK keys. The last one cannot go.</summary>
    public async Task DeleteAsync(
        string projectKey, string environmentKey, ConfirmKeyRequest request, Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.EnsureMatches(environmentKey);
        var (change, revokedKeys) = await db.ExecuteInTransactionAsync(
            async token =>
            {
                var project = await db.GetProjectAsync(projectKey, token);
                var environment = await db.GetEnvironmentAsync(project, environmentKey, token);
                if (!await db.Environments.AnyAsync(e => e.ProjectId == project.Id && e.Id != environment.Id, token))
                {
                    throw new ConflictException("A project needs at least one environment, so its last environment cannot be deleted.");
                }

                var sdkKeyIds = await db.SdkKeys
                    .Where(k => k.EnvironmentId == environment.Id && k.RevokedAt == null)
                    .Select(k => k.Id)
                    .ToListAsync(token);
                var version = await db.IncrementConfigVersionAsync(environment.Id, token);
                await db.FlagUsageHourly.Where(u => u.EnvironmentId == environment.Id).ExecuteDeleteAsync(token);
                await db.ScheduledChanges.Where(s => s.EnvironmentId == environment.Id).ExecuteDeleteAsync(token);
                await db.FlagEnvironmentConfigs.Where(c => c.EnvironmentId == environment.Id).ExecuteDeleteAsync(token);
                await db.Environments.Where(e => e.Id == environment.Id).ExecuteDeleteAsync(token);
                audit.Record(actor, AuditActions.EnvironmentDeleted, AuditTarget.For(project, environment), before: Snapshot(environment));
                await db.SaveChangesAsync(token);
                return (new ConfigChangedMessage(environment.Id, version), sdkKeyIds);
            },
            cancellationToken);

        await notifier.PublishSdkKeyRevokedAsync(revokedKeys, cancellationToken);
        await notifier.PublishConfigChangedAsync([change], cancellationToken);
    }

    private static object Snapshot(ProjectEnvironment environment) =>
        new { environment.Key, environment.Name, environment.Color, environment.IsProtected, environment.SortOrder };
}
