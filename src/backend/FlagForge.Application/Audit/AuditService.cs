using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Audit;

/// <summary>Reads the audit log, newest first. Keys resolve to ids, which the audit indexes are built on.</summary>
public sealed class AuditService(IFlagForgeDbContext db, IValidator<AuditQuery> validator)
{
    public async Task<PagedResult<AuditEntryResponse>> ListAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(query, cancellationToken);
        var entries = db.AuditEntries.AsNoTracking();
        if (query.ProjectKey is not null)
        {
            var scope = await ResolveAsync(query, cancellationToken);
            if (scope is null)
            {
                // Filtering by something that no longer exists matches nothing.
                return new PagedResult<AuditEntryResponse>([], query.Page, query.PageSize, 0);
            }

            var (projectId, flagId, environmentId) = scope.Value;
            entries = entries.Where(a => a.ProjectId == projectId);
            if (flagId is not null)
            {
                entries = entries.Where(a => a.FlagId == flagId);
            }

            if (environmentId is not null)
            {
                entries = entries.Where(a => a.EnvironmentId == environmentId);
            }
        }

        if (query.ActorId is not null)
        {
            entries = entries.Where(a => a.ActorId == query.ActorId);
        }

        if (query.Action is not null)
        {
            entries = entries.Where(a => a.Action == query.Action);
        }

        if (query.From is not null)
        {
            entries = entries.Where(a => a.OccurredAt >= query.From);
        }

        if (query.To is not null)
        {
            entries = entries.Where(a => a.OccurredAt < query.To);
        }

        var total = await entries.CountAsync(cancellationToken);
        var page = await entries
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)
            .Page(query.Page, query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AuditEntryResponse>([.. page.Select(AuditEntryResponse.From)], query.Page, query.PageSize, total);
    }

    private async Task<(Guid ProjectId, Guid? FlagId, Guid? EnvironmentId)?> ResolveAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Key == query.ProjectKey, cancellationToken);
        if (project is null)
        {
            return null;
        }

        Guid? flagId = null;
        if (query.FlagKey is not null)
        {
            flagId = await db.Flags.AsNoTracking()
                .Where(f => f.ProjectId == project.Id && f.Key == query.FlagKey)
                .Select(f => (Guid?)f.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (flagId is null)
            {
                return null;
            }
        }

        Guid? environmentId = null;
        if (query.EnvironmentKey is not null)
        {
            environmentId = await db.Environments.AsNoTracking()
                .Where(e => e.ProjectId == project.Id && e.Key == query.EnvironmentKey)
                .Select(e => (Guid?)e.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (environmentId is null)
            {
                return null;
            }
        }

        return (project.Id, flagId, environmentId);
    }
}
