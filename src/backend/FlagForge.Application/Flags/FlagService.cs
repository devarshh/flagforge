using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.Application.Stale;
using FlagForge.Application.Targeting;
using FlagForge.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Flags;

/// <summary>
/// Flag lifecycle. Anything that changes evaluation output (creating a flag, changing variation values, archiving,
/// restoring, deleting) bumps every environment's <c>ConfigVersion</c> and publishes after commit.
/// </summary>
public sealed class FlagService(
    IFlagForgeDbContext db,
    IAuditWriter audit,
    IChangeNotifier notifier,
    StaleFlagService staleFlags,
    TimeProvider timeProvider,
    IValidator<FlagListQuery> listValidator,
    IValidator<CreateFlagRequest> createValidator,
    IValidator<UpdateFlagRequest> updateValidator)
{
    public async Task<PagedResult<FlagSummaryResponse>> ListAsync(string projectKey, FlagListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken);
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flags = db.Flags.AsNoTracking().Where(f => f.ProjectId == project.Id);
        if (!query.IncludeArchived)
        {
            flags = flags.Where(f => !f.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            flags = flags.Where(f => f.Key.Contains(search) || f.Name.Contains(search) || (f.Description != null && f.Description.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim();
            flags = flags.Where(f => f.Tags.Contains(tag));
        }

        var total = await flags.CountAsync(cancellationToken);
        var page = await flags
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.Key)
            .Page(query.Page, query.PageSize)
            .ToListAsync(cancellationToken);

        var flagIds = page.Select(f => f.Id).ToList();
        var environments = await db.Environments.AsNoTracking()
            .Where(e => e.ProjectId == project.Id)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Key)
            .Select(e => new { e.Id, e.Key })
            .ToListAsync(cancellationToken);
        var configs = (await db.FlagEnvironmentConfigs.AsNoTracking()
                .Where(c => flagIds.Contains(c.FlagId))
                .Select(c => new { c.FlagId, c.EnvironmentId, c.Enabled, c.Version })
                .ToListAsync(cancellationToken))
            .ToDictionary(c => (c.FlagId, c.EnvironmentId));
        var lastEvaluated = await LastEvaluatedAsync(flagIds, cancellationToken);
        var stale = (await staleFlags.FindAsync(project.Id, cancellationToken)).Select(r => r.FlagKey).ToHashSet(StringComparer.Ordinal);

        var items = page
            .Select(flag => new FlagSummaryResponse(
                flag.Id,
                flag.Key,
                flag.Name,
                flag.Description,
                flag.Type,
                flag.Tags,
                flag.IsPermanent,
                flag.IsArchived,
                stale.Contains(flag.Key),
                flag.CreatedAt,
                flag.UpdatedAt,
                [.. environments
                    .Where(e => configs.ContainsKey((flag.Id, e.Id)))
                    .Select(e =>
                    {
                        var config = configs[(flag.Id, e.Id)];
                        return new FlagEnvironmentSummary(e.Key, config.Enabled, config.Version, LastEvaluatedAt(lastEvaluated, flag.Id, e.Id));
                    })]))
            .ToList();
        return new PagedResult<FlagSummaryResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<FlagResponse> GetAsync(string projectKey, string flagKey, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.Flags.AsNoTracking().FirstOrDefaultAsync(f => f.ProjectId == project.Id && f.Key == flagKey, cancellationToken)
            ?? throw new NotFoundException($"Flag '{flagKey}' does not exist in project '{project.Key}'.");
        return await ToResponseAsync(flag, cancellationToken);
    }

    /// <summary>Creates the flag with a default (disabled) config in every environment of the project.</summary>
    public async Task<FlagResponse> CreateAsync(string projectKey, CreateFlagRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        var (flag, changes) = await db.ExecuteInTransactionAsync(
            async token =>
            {
                var project = await db.GetProjectAsync(projectKey, token);
                if (await db.Flags.AnyAsync(f => f.ProjectId == project.Id && f.Key == request.Key, token))
                {
                    throw new ConflictException($"Project '{project.Key}' already has a flag with the key '{request.Key}'.");
                }

                var now = timeProvider.GetUtcNow();
                var flag = new Flag
                {
                    Id = Guid.CreateVersion7(now),
                    ProjectId = project.Id,
                    Key = request.Key,
                    Name = request.Name.Trim(),
                    Description = NullIfBlank(request.Description),
                    Type = request.Type,
                    Variations = request.Type == FlagType.Boolean ? Variation.ForBoolean() : ToVariations(request.Variations!, []),
                    Tags = TagRules.Normalize(request.Tags),
                    Salt = Generate.Salt(),
                    IsPermanent = request.IsPermanent,
                    CreatedAt = now,
                    CreatedByUserId = actor.UserId ?? throw new InvalidOperationException("Flags are created by users."),
                    UpdatedAt = now,
                };
                db.Flags.Add(flag);

                var environmentIds = await db.Environments.Where(e => e.ProjectId == project.Id).Select(e => e.Id).ToListAsync(token);
                db.FlagEnvironmentConfigs.AddRange(environmentIds.Select(environmentId =>
                    FlagEnvironmentConfig.CreateDefault(Guid.CreateVersion7(now), flag, environmentId, now)));

                audit.Record(actor, AuditActions.FlagCreated, AuditTarget.For(project, flag), after: Snapshot(flag));
                await db.SaveChangesAsync(token);

                // A new flag appears in every environment's evaluation output (serving its off variation).
                return (flag, await db.IncrementProjectAsync(project.Id, token));
            },
            cancellationToken);

        await notifier.PublishConfigChangedAsync(changes, cancellationToken);
        return await ToResponseAsync(flag, cancellationToken);
    }

    public async Task<FlagResponse> UpdateAsync(string projectKey, string flagKey, UpdateFlagRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken);
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.GetFlagAsync(project, flagKey, cancellationToken);
        var before = Snapshot(flag);
        flag.Name = request.Name?.Trim() ?? flag.Name;
        flag.Description = request.Description is null ? flag.Description : NullIfBlank(request.Description);
        flag.Tags = request.Tags is null ? flag.Tags : TagRules.Normalize(request.Tags);
        flag.IsPermanent = request.IsPermanent ?? flag.IsPermanent;
        flag.UpdatedAt = timeProvider.GetUtcNow();
        audit.Record(actor, AuditActions.FlagUpdated, AuditTarget.For(project, flag), before: before, after: Snapshot(flag));
        await db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(flag, cancellationToken);
    }

    /// <summary>
    /// Replaces the variation list of a non-boolean flag. Existing ids keep their identity; a variation still served
    /// by any environment or pending scheduled change cannot be removed (409). Value changes bump every environment.
    /// </summary>
    public async Task<FlagResponse> ReplaceVariationsAsync(
        string projectKey, string flagKey, ReplaceVariationsRequest request, Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (flag, changes) = await db.ExecuteInTransactionAsync(
            async token =>
            {
                var project = await db.GetProjectAsync(projectKey, token);
                var flag = await db.GetFlagAsync(project, flagKey, token);
                if (flag.Type == FlagType.Boolean)
                {
                    throw RequestValidationException.For("variations", "Boolean flags always serve true or false, so their variations cannot change.");
                }

                if (flag.IsArchived)
                {
                    throw new ConflictException("This flag is archived. Restore it before changing its variations.");
                }

                var existing = flag.Variations.ToDictionary(v => v.Id, StringComparer.Ordinal);
                EnsureValidReplacement(flag.Type, request.Variations, existing);
                var keptIds = request.Variations.Where(v => v.Id is not null).Select(v => v.Id!).ToHashSet(StringComparer.Ordinal);
                await EnsureRemovableAsync(flag, [.. existing.Keys.Where(id => !keptIds.Contains(id))], token);

                var variations = ToVariations(request.Variations, existing.Keys);
                var valuesChanged = variations.Any(v => existing.TryGetValue(v.Id, out var old) && !JsonElement.DeepEquals(old.Value, v.Value));
                var before = flag.Variations;
                flag.Variations = variations;
                flag.UpdatedAt = timeProvider.GetUtcNow();
                audit.Record(actor, AuditActions.FlagVariationsUpdated, AuditTarget.For(project, flag),
                    before: new { Variations = before }, after: new { Variations = variations });
                await db.SaveChangesAsync(token);
                return (flag, valuesChanged ? await db.IncrementProjectAsync(project.Id, token) : []);
            },
            cancellationToken);

        await notifier.PublishConfigChangedAsync(changes, cancellationToken);
        return await ToResponseAsync(flag, cancellationToken);
    }

    public Task<FlagResponse> ArchiveAsync(string projectKey, string flagKey, Actor actor, CancellationToken cancellationToken) =>
        SetArchivedAsync(projectKey, flagKey, archived: true, actor, cancellationToken);

    public Task<FlagResponse> RestoreAsync(string projectKey, string flagKey, Actor actor, CancellationToken cancellationToken) =>
        SetArchivedAsync(projectKey, flagKey, archived: false, actor, cancellationToken);

    /// <summary>Permanently deletes an archived flag with its configs, schedules, and usage. History remains.</summary>
    public async Task DeleteAsync(string projectKey, string flagKey, ConfirmKeyRequest request, Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.EnsureMatches(flagKey);
        var changes = await db.ExecuteInTransactionAsync(
            async token =>
            {
                var project = await db.GetProjectAsync(projectKey, token);
                var flag = await db.GetFlagAsync(project, flagKey, token);
                if (!flag.IsArchived)
                {
                    throw new ConflictException("Archive the flag before deleting it.");
                }

                await db.FlagUsageHourly.Where(u => u.FlagId == flag.Id).ExecuteDeleteAsync(token);
                await db.Flags.Where(f => f.Id == flag.Id).ExecuteDeleteAsync(token);
                audit.Record(actor, AuditActions.FlagDeleted, AuditTarget.For(project, flag), before: Snapshot(flag));
                await db.SaveChangesAsync(token);
                return await db.IncrementProjectAsync(project.Id, token);
            },
            cancellationToken);

        await notifier.PublishConfigChangedAsync(changes, cancellationToken);
    }

    /// <summary>Archiving also cancels the flag's pending scheduled changes, so nothing fires for an archived flag.</summary>
    private async Task<FlagResponse> SetArchivedAsync(string projectKey, string flagKey, bool archived, Actor actor, CancellationToken cancellationToken)
    {
        var (flag, changes) = await db.ExecuteInTransactionAsync(
            async token =>
            {
                var project = await db.GetProjectAsync(projectKey, token);
                var flag = await db.GetFlagAsync(project, flagKey, token);
                if (flag.IsArchived == archived)
                {
                    return (flag, new List<ConfigChangedMessage>());
                }

                var now = timeProvider.GetUtcNow();
                flag.IsArchived = archived;
                flag.ArchivedAt = archived ? now : null;
                flag.UpdatedAt = now;
                if (archived)
                {
                    var pending = await db.ScheduledChanges
                        .Include(s => s.Environment)
                        .Where(s => s.FlagId == flag.Id && s.Status == ScheduledChangeStatus.Pending)
                        .ToListAsync(token);
                    foreach (var change in pending)
                    {
                        change.Status = ScheduledChangeStatus.Cancelled;
                        audit.Record(actor, AuditActions.ScheduleCancelled, AuditTarget.For(project, flag, change.Environment),
                            "Cancelled because the flag was archived.", before: new { change.Id, change.Action, change.ExecuteAt });
                    }
                }

                audit.Record(actor, archived ? AuditActions.FlagArchived : AuditActions.FlagRestored, AuditTarget.For(project, flag));
                await db.SaveChangesAsync(token);
                return (flag, await db.IncrementProjectAsync(project.Id, token));
            },
            cancellationToken);

        await notifier.PublishConfigChangedAsync(changes, cancellationToken);
        return await ToResponseAsync(flag, cancellationToken);
    }

    private static void EnsureValidReplacement(FlagType type, IReadOnlyList<VariationInput> variations, Dictionary<string, Variation> existing)
    {
        var errors = VariationRules.Check(type, variations).ToList();
        if (variations is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < variations.Count; i++)
            {
                if (variations[i]?.Id is not { } id)
                {
                    continue;
                }

                if (!existing.ContainsKey(id))
                {
                    errors.Add(($"variations[{i}].id", $"Variation '{id}' does not exist on this flag. Leave the id out for new variations."));
                }
                else if (!seen.Add(id))
                {
                    errors.Add(($"variations[{i}].id", $"Variation '{id}' appears more than once."));
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors
                .GroupBy(e => e.Path)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).Distinct().ToArray()));
        }
    }

    private async Task EnsureRemovableAsync(Flag flag, IReadOnlyList<string> removedIds, CancellationToken cancellationToken)
    {
        if (removedIds.Count == 0)
        {
            return;
        }

        var configs = await db.FlagEnvironmentConfigs.AsNoTracking()
            .Include(c => c.Environment)
            .Where(c => c.FlagId == flag.Id)
            .ToListAsync(cancellationToken);
        foreach (var variationId in removedIds)
        {
            var name = flag.Variations.First(v => v.Id == variationId).Name;
            var servedIn = configs.Where(c => c.References(variationId)).Select(c => c.Environment.Name).ToList();
            if (servedIn.Count > 0)
            {
                throw new ConflictException(
                    $"'{name}' is still served in {string.Join(", ", servedIn)}. Change the targeting there first, then remove the variation.");
            }
        }

        var openPayloads = await db.ScheduledChanges.AsNoTracking()
            .Where(s => s.FlagId == flag.Id && (s.Status == ScheduledChangeStatus.Pending || s.Status == ScheduledChangeStatus.Processing))
            .Select(s => s.Payload)
            .ToListAsync(cancellationToken);
        var scheduled = removedIds.FirstOrDefault(id => openPayloads.Any(p => p is not null && p.References(id)));
        if (scheduled is not null)
        {
            var name = flag.Variations.First(v => v.Id == scheduled).Name;
            throw new ConflictException($"A pending scheduled change still serves '{name}'. Cancel it first, then remove the variation.");
        }
    }

    private static List<Variation> ToVariations(IReadOnlyList<VariationInput> inputs, IEnumerable<string> existingIds)
    {
        var usedIds = new HashSet<string>(existingIds, StringComparer.Ordinal);
        usedIds.UnionWith(inputs.Where(v => v.Id is not null).Select(v => v.Id!));
        return
        [
            .. inputs.Select(input => new Variation
            {
                Id = input.Id ?? NewVariationId(usedIds),
                Name = input.Name.Trim(),
                Value = input.Value.Clone(),
                Description = NullIfBlank(input.Description),
            }),
        ];
    }

    private static string NewVariationId(HashSet<string> usedIds)
    {
        string id;
        do
        {
            id = Generate.VariationId();
        }
        while (!usedIds.Add(id));
        return id;
    }

    private async Task<FlagResponse> ToResponseAsync(Flag flag, CancellationToken cancellationToken)
    {
        var environments = await db.Environments.AsNoTracking()
            .Where(e => e.ProjectId == flag.ProjectId)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Key)
            .ToListAsync(cancellationToken);
        var configs = await db.FlagEnvironmentConfigs.AsNoTracking()
            .Where(c => c.FlagId == flag.Id)
            .ToDictionaryAsync(c => c.EnvironmentId, cancellationToken);
        var userIds = configs.Values.Where(c => c.UpdatedByUserId is not null).Select(c => c.UpdatedByUserId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserRef(u.Id, u.DisplayName))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
        var lastEvaluated = await LastEvaluatedAsync([flag.Id], cancellationToken);

        return new FlagResponse(
            flag.Id,
            flag.Key,
            flag.Name,
            flag.Description,
            flag.Type,
            flag.Variations,
            flag.Tags,
            flag.IsPermanent,
            flag.IsArchived,
            flag.ArchivedAt,
            flag.CreatedAt,
            flag.UpdatedAt,
            [.. environments
                .Where(e => configs.ContainsKey(e.Id))
                .Select(e =>
                {
                    var config = configs[e.Id];
                    var updatedBy = config.UpdatedByUserId is { } id ? users.GetValueOrDefault(id) : null;
                    return new FlagEnvironmentResponse(e.Key, LastEvaluatedAt(lastEvaluated, flag.Id, e.Id), TargetingResponse.From(config, e.Key, updatedBy));
                })]);
    }

    /// <summary>The start of the most recent hour with evaluations, per flag and environment.</summary>
    private async Task<Dictionary<(Guid FlagId, Guid EnvironmentId), DateTimeOffset>> LastEvaluatedAsync(
        IReadOnlyCollection<Guid> flagIds, CancellationToken cancellationToken)
    {
        var rows = await db.FlagUsageHourly.AsNoTracking()
            .Where(u => flagIds.Contains(u.FlagId))
            .GroupBy(u => new { u.FlagId, u.EnvironmentId })
            .Select(g => new { g.Key.FlagId, g.Key.EnvironmentId, Last = g.Max(u => u.HourStart) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => (r.FlagId, r.EnvironmentId), r => r.Last);
    }

    private static DateTimeOffset? LastEvaluatedAt(Dictionary<(Guid, Guid), DateTimeOffset> lastEvaluated, Guid flagId, Guid environmentId) =>
        lastEvaluated.TryGetValue((flagId, environmentId), out var at) ? at : null;

    private static object Snapshot(Flag flag) => new
    {
        flag.Key,
        flag.Name,
        flag.Description,
        flag.Type,
        Variations = flag.Variations.Select(v => v.Id),
        flag.Tags,
        flag.IsPermanent,
    };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
