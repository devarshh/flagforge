using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.SdkKeys;

/// <summary>SDK keys are hashed at rest (SHA-256) and treated as public identifiers (ADR 0010).</summary>
public sealed class SdkKeyService(
    IFlagForgeDbContext db,
    IAuditWriter audit,
    IChangeNotifier notifier,
    TimeProvider timeProvider,
    IValidator<CreateSdkKeyRequest> createValidator)
{
    public async Task<IReadOnlyList<SdkKeyResponse>> ListAsync(string projectKey, string environmentKey, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var keys = await db.SdkKeys.AsNoTracking()
            .Where(k => k.EnvironmentId == environment.Id)
            .OrderBy(k => k.RevokedAt != null)
            .ThenByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
        return [.. keys.Select(SdkKeyResponse.From)];
    }

    public async Task<SdkKeyResponse> GetAsync(string projectKey, string environmentKey, Guid keyId, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var key = await db.SdkKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == keyId && k.EnvironmentId == environment.Id, cancellationToken)
            ?? throw new NotFoundException("That SDK key does not exist in this environment.");
        return SdkKeyResponse.From(key);
    }

    public async Task<CreatedSdkKeyResponse> CreateAsync(
        string projectKey, string environmentKey, CreateSdkKeyRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var plaintext = Generate.SdkKey();
        var key = new SdkKey
        {
            Id = Guid.CreateVersion7(now),
            EnvironmentId = environment.Id,
            Name = request.Name.Trim(),
            KeyPrefix = SdkKey.DisplayPrefixOf(plaintext),
            KeyHash = Hashing.Sha256Hex(plaintext),
            CreatedAt = now,
            CreatedByUserId = actor.UserId ?? throw new InvalidOperationException("SDK keys are created by users."),
        };
        db.SdkKeys.Add(key);
        audit.Record(actor, AuditActions.SdkKeyCreated, AuditTarget.For(project, environment), after: new { key.Id, key.Name, key.KeyPrefix });
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedSdkKeyResponse(key.Id, key.Name, key.KeyPrefix, key.CreatedAt, plaintext);
    }

    /// <summary>Soft-revokes the key and tells every evaluation API pod to forget it.</summary>
    public async Task RevokeAsync(string projectKey, string environmentKey, Guid keyId, Actor actor, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var key = await db.SdkKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.EnvironmentId == environment.Id, cancellationToken)
            ?? throw new NotFoundException("That SDK key does not exist in this environment.");
        if (key.RevokedAt is not null)
        {
            return;
        }

        key.RevokedAt = timeProvider.GetUtcNow();
        audit.Record(actor, AuditActions.SdkKeyRevoked, AuditTarget.For(project, environment), before: new { key.Id, key.Name, key.KeyPrefix });
        await db.SaveChangesAsync(cancellationToken);
        await notifier.PublishSdkKeyRevokedAsync([key.Id], cancellationToken);
    }
}
