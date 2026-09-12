namespace DesignPatterns.Structural.Adapter.ExternalSdks;

/// <summary>
/// Stand ins for third party SDKs. Each one has its own vocabulary, its own
/// status values and its own money representation, exactly as real ones do.
/// You do not get to change these.
/// </summary>
public sealed class AsaasClient
{
    public AsaasPaymentResponse CreatePayment(AsaasPaymentRequest request) =>
        new()
        {
            Id = $"pay_{Guid.NewGuid():N}",
            Status = "PENDING",
            Value = request.Value,
            DueDate = request.DueDate,
            BillingType = request.BillingType
        };

    public AsaasPaymentResponse GetPayment(string id) =>
        new() { Id = id, Status = "RECEIVED", Value = 100m, DueDate = DateTime.UtcNow, BillingType = "PIX" };
}

public sealed class AsaasPaymentRequest
{
    public string Customer { get; init; } = string.Empty;

    /// <summary>Amount in reais, as a decimal.</summary>
    public decimal Value { get; init; }

    public DateTime DueDate { get; init; }

    /// <summary>PIX, BOLETO or CREDIT_CARD.</summary>
    public string BillingType { get; init; } = string.Empty;
}

public sealed class AsaasPaymentResponse
{
    public string Id { get; init; } = string.Empty;

    /// <summary>PENDING, RECEIVED, OVERDUE or REFUNDED.</summary>
    public string Status { get; init; } = string.Empty;

    public decimal Value { get; init; }

    public DateTime DueDate { get; init; }

    public string BillingType { get; init; } = string.Empty;
}
