using Xunit;

namespace FlagForge.Testing;

/// <summary>Base class for integration tests: resets the database before each test.</summary>
public abstract class IntegrationTest(FlagForgeFixture fixture) : IAsyncLifetime
{
    protected FlagForgeFixture Fixture { get; } = fixture;

    /// <summary>The test's cancellation token; pass it to every call that accepts one.</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync() => await Fixture.ResetAsync();

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
