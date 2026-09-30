using FlagForge.Application.Common;
using FlagForge.Application.Environments;
using FlagForge.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Projects;

public sealed class ProjectService(
    IFlagForgeDbContext db,
    IAuditWriter audit,
    IChangeNotifier notifier,
    TimeProvider timeProvider,
    IValidator<CreateProjectRequest> createValidator,
    IValidator<UpdateProjectRequest> updateValidator)
{
    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var projects = await db.Projects.AsNoTracking()
            .Include(p => p.Environments)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        return [.. projects.Select(p => ToResponse(p, p.Environments))];
    }

    public async Task<ProjectResponse> GetAsync(string projectKey, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking()
            .Include(p => p.Environments)
            .FirstOrDefaultAsync(p => p.Key == projectKey, cancellationToken)
            ?? throw new NotFoundException($"Project '{projectKey}' does not exist.");
        return ToResponse(project, project.Environments);
    }

    /// <summary>Creates the project with development, staging, and production (protected) environments.</summary>
    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        if (await db.Projects.AnyAsync(p => p.Key == request.Key, cancellationToken))
        {
            throw new ConflictException($"A project with the key '{request.Key}' already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var project = new Project
        {
            Id = Guid.CreateVersion7(now),
            Key = request.Key,
            Name = request.Name.Trim(),
            Description = NullIfBlank(request.Description),
            CreatedAt = now,
            CreatedByUserId = actor.UserId ?? throw new InvalidOperationException("Projects are created by users."),
        };
        var environments = EnvironmentDefaults.Initial
            .Select((env, index) => new ProjectEnvironment
            {
                Id = Guid.CreateVersion7(now),
                ProjectId = project.Id,
                Key = env.Key,
                Name = env.Name,
                Color = env.Color,
                IsProtected = env.IsProtected,
                SortOrder = index,
                CreatedAt = now,
            })
            .ToList();
        db.Projects.Add(project);
        db.Environments.AddRange(environments);
        audit.Record(actor, AuditActions.ProjectCreated, AuditTarget.For(project), after: new
        {
            project.Key,
            project.Name,
            project.Description,
            Environments = environments.Select(e => e.Key),
        });
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(project, environments);
    }

    public async Task<ProjectResponse> UpdateAsync(string projectKey, UpdateProjectRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken);
        var project = await db.Projects.Include(p => p.Environments).FirstOrDefaultAsync(p => p.Key == projectKey, cancellationToken)
            ?? throw new NotFoundException($"Project '{projectKey}' does not exist.");
        var before = Snapshot(project);
        project.Name = request.Name?.Trim() ?? project.Name;
        project.Description = request.Description is null ? project.Description : NullIfBlank(request.Description);
        audit.Record(actor, AuditActions.ProjectUpdated, AuditTarget.For(project), before: before, after: Snapshot(project));
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(project, project.Environments);
    }

    /// <summary>Deletes the project and everything in it. SDK keys stop working immediately.</summary>
    public async Task DeleteAsync(string projectKey, ConfirmKeyRequest request, Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.EnsureMatches(projectKey);
        var (changes, revokedKeys) = await db.ExecuteInTransactionAsync(
            async token =>
            {
                var project = await db.GetProjectAsync(projectKey, token);
                var environmentIds = await db.Environments.Where(e => e.ProjectId == project.Id).Select(e => e.Id).ToListAsync(token);
                var sdkKeyIds = await db.SdkKeys
                    .Where(k => environmentIds.Contains(k.EnvironmentId) && k.RevokedAt == null)
                    .Select(k => k.Id)
                    .ToListAsync(token);
                var changes = new List<ConfigChangedMessage>();
                foreach (var environmentId in environmentIds)
                {
                    changes.Add(new ConfigChangedMessage(environmentId, await db.IncrementConfigVersionAsync(environmentId, token)));
                }

                await db.FlagUsageHourly.Where(u => environmentIds.Contains(u.EnvironmentId)).ExecuteDeleteAsync(token);
                await db.ScheduledChanges.Where(s => environmentIds.Contains(s.EnvironmentId)).ExecuteDeleteAsync(token);
                await db.FlagEnvironmentConfigs.Where(c => environmentIds.Contains(c.EnvironmentId)).ExecuteDeleteAsync(token);
                await db.Projects.Where(p => p.Id == project.Id).ExecuteDeleteAsync(token);
                audit.Record(actor, AuditActions.ProjectDeleted, AuditTarget.For(project), before: Snapshot(project));
                await db.SaveChangesAsync(token);
                return (changes, sdkKeyIds);
            },
            cancellationToken);

        await notifier.PublishSdkKeyRevokedAsync(revokedKeys, cancellationToken);
        await notifier.PublishConfigChangedAsync(changes, cancellationToken);
    }

    private static ProjectResponse ToResponse(Project project, IEnumerable<ProjectEnvironment> environments) =>
        new(
            project.Id,
            project.Key,
            project.Name,
            project.Description,
            project.CreatedAt,
            [.. environments.OrderBy(e => e.SortOrder).ThenBy(e => e.Key).Select(EnvironmentResponse.From)]);

    private static object Snapshot(Project project) => new { project.Key, project.Name, project.Description };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
