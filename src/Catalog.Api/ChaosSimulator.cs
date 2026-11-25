namespace Catalog.Api;

/// <summary>
/// Causes problems on purpose, so you can practise diagnosing them with traces,
/// logs and metrics. Order product 98 or 99 to trigger one.
/// This is for learning only and does not belong in a real product.
/// </summary>
public static class ChaosSimulator
{
    public const int SlowProductId = 98;
    public const int BrokenProductId = 99;

    private static readonly TimeSpan SlowDelay = TimeSpan.FromSeconds(3);

    public static async Task ApplyAsync(int productId, CancellationToken cancellationToken)
    {
        if (productId == SlowProductId)
        {
            await Task.Delay(SlowDelay, cancellationToken);
        }

        if (productId == BrokenProductId)
        {
            throw new InvalidOperationException("Simulated failure: the Broken Product always fails.");
        }
    }
}
