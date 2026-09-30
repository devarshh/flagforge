namespace FlagForge.Application.Common;

/// <summary>Body for destructive operations: the caller retypes the resource key to confirm.</summary>
public sealed record ConfirmKeyRequest(string ConfirmKey)
{
    public void EnsureMatches(string key)
    {
        if (!string.Equals(ConfirmKey, key, StringComparison.Ordinal))
        {
            throw RequestValidationException.For("confirmKey", $"Type '{key}' to confirm.");
        }
    }
}
