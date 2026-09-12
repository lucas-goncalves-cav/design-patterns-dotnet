using DesignPatterns.Structural.Adapter.ExternalSdks;

namespace DesignPatterns.Structural.Adapter.GoodExample;

/// <summary>
/// Translates between the Asaas vocabulary and this application's vocabulary.
/// Everything Asaas specific stops at this class.
/// </summary>
public sealed class AsaasGatewayAdapter : IPaymentGateway
{
    private readonly AsaasClient _client;

    public AsaasGatewayAdapter(AsaasClient client)
    {
        _client = client;
    }

    public string Name => "asaas";

    public Payment Create(CreatePaymentCommand command)
    {
        var response = _client.CreatePayment(new AsaasPaymentRequest
        {
            Customer = command.CustomerReference,
            Value = command.Amount,
            DueDate = command.DueDate.ToDateTime(TimeOnly.MinValue),
            BillingType = ToAsaasBillingType(command.Method)
        });

        return ToPayment(response);
    }

    public Payment GetById(string externalId) => ToPayment(_client.GetPayment(externalId));

    private static Payment ToPayment(AsaasPaymentResponse response) =>
        new(
            ExternalId: response.Id,
            Status: ToPaymentStatus(response.Status),
            Amount: response.Value,
            Method: ToPaymentMethod(response.BillingType));

    private static string ToAsaasBillingType(PaymentMethod method) => method switch
    {
        PaymentMethod.Pix => "PIX",
        PaymentMethod.Boleto => "BOLETO",
        PaymentMethod.CreditCard => "CREDIT_CARD",
        _ => throw new NotSupportedException($"Asaas does not support {method}.")
    };

    private static PaymentMethod ToPaymentMethod(string billingType) => billingType switch
    {
        "PIX" => PaymentMethod.Pix,
        "BOLETO" => PaymentMethod.Boleto,
        "CREDIT_CARD" => PaymentMethod.CreditCard,
        _ => throw new NotSupportedException($"Unknown Asaas billing type: {billingType}.")
    };

    /// <summary>
    /// An unrecognised status throws rather than defaulting to Pending.
    /// A provider adding a status must be a visible failure, not a payment
    /// silently treated as unpaid.
    /// </summary>
    private static PaymentStatus ToPaymentStatus(string status) => status switch
    {
        "PENDING" or "AWAITING_RISK_ANALYSIS" => PaymentStatus.Pending,
        "RECEIVED" or "CONFIRMED" or "RECEIVED_IN_CASH" => PaymentStatus.Paid,
        "OVERDUE" => PaymentStatus.Overdue,
        "REFUNDED" or "REFUND_REQUESTED" => PaymentStatus.Refunded,
        "DELETED" => PaymentStatus.Cancelled,
        _ => throw new NotSupportedException($"Unmapped Asaas status: {status}.")
    };
}
