namespace FlagForge.EvaluationApi.Hubs;

/// <summary>Server-to-client messages. The hub has no client-to-server methods.</summary>
public interface IFlagsClient
{
    /// <summary>Something that affects this environment's evaluations changed; clients re-evaluate.</summary>
    Task FlagsChanged(FlagsChangedMessage message);
}

/// <summary>Carries only a version, never flag data: clients fetch values through the authenticated REST endpoint.</summary>
public sealed record FlagsChangedMessage(long EnvironmentVersion);
