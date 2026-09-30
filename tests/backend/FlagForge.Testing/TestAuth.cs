namespace FlagForge.Testing;

public static class TestAuth
{
    /// <summary>A fixed signing key for test hosts only.</summary>
    public const string SigningKey = "integration-tests-only-signing-key-0123456789";

    /// <summary>The password of every user created by <see cref="TestUsers"/>.</summary>
    public const string Password = "Correct-Horse-Battery-9";
}
