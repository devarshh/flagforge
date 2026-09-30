namespace FlagForge.Testing;

/// <summary>Polls instead of sleeping: asynchronous effects are awaited with a deadline, never a fixed delay.</summary>
public static class Eventually
{
    public static async Task<T> GetAsync<T>(
        Func<Task<T>> probe, Func<T, bool> isDone, TimeSpan timeout, CancellationToken cancellationToken, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(isDone);
        var deadline = TimeProvider.System.GetUtcNow() + timeout;
        while (true)
        {
            var value = await probe();
            if (isDone(value))
            {
                return value;
            }

            if (TimeProvider.System.GetUtcNow() >= deadline)
            {
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0.#} s waiting for {description ?? "the condition"}. Last value: {value}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    public static Task AssertAsync(Func<Task<bool>> condition, TimeSpan timeout, CancellationToken cancellationToken, string? description = null) =>
        GetAsync(condition, done => done, timeout, cancellationToken, description);
}
