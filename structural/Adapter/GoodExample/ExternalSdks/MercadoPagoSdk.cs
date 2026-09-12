namespace DesignPatterns.Structural.Adapter.ExternalSdks;

public sealed class MercadoPagoClient
{
    public MpPayment Post(MpPaymentPayload payload) =>
        new()
        {
            IdPagamento = Random.Shared.NextInt64(1_000_000, 9_999_999),
            StatusPagamento = "pending",
            ValorTransacao = payload.TransactionAmount,
            MeioPagamento = payload.PaymentMethodId
        };

    public MpPayment Get(long id) =>
        new() { IdPagamento = id, StatusPagamento = "approved", ValorTransacao = 100d, MeioPagamento = "pix" };
}

public sealed class MpPaymentPayload
{
    /// <summary>Amount as a double, because the API is JSON first.</summary>
    public double TransactionAmount { get; init; }

    public string PaymentMethodId { get; init; } = string.Empty;

    public string PayerEmail { get; init; } = string.Empty;
}

public sealed class MpPayment
{
    /// <summary>A numeric id, not a string.</summary>
    public long IdPagamento { get; init; }

    /// <summary>pending, approved, rejected or refunded.</summary>
    public string StatusPagamento { get; init; } = string.Empty;

    public double ValorTransacao { get; init; }

    public string MeioPagamento { get; init; } = string.Empty;
}
