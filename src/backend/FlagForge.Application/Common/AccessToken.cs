namespace FlagForge.Application.Common;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
