namespace DesignPatterns.Structural.Adapter.ExternalSdks;

public sealed class StripeClient
{
    public StripePaymentIntent Create(StripePaymentIntentOptions options) =>
        new()
        {
            Id = $"pi_{Guid.NewGuid():N}",
            Status = "requires_payment_method",
            Amount = options.Amount,
            Currency = options.Currency
        };

    public StripePaymentIntent Retrieve(string id) =>
        new() { Id = id, Status = "succeeded", Amount = 10_000, Currency = "brl" };
}

public sealed class StripePaymentIntentOptions
{
    /// <summary>Amount in the smallest currency unit. Cents, not reais.</summary>
    public long Amount { get; init; }

    public string Currency { get; init; } = "brl";

    public string? Customer { get; init; }
}

public sealed class StripePaymentIntent
{
    public string Id { get; init; } = string.Empty;

    /// <summary>requires_payment_method, processing, succeeded or canceled.</summary>
    public string Status { get; init; } = string.Empty;

    public long Amount { get; init; }

    public string Currency { get; init; } = string.Empty;
}
