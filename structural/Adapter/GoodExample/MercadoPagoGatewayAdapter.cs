using DesignPatterns.Structural.Adapter.ExternalSdks;

namespace DesignPatterns.Structural.Adapter.GoodExample;

public sealed class MercadoPagoGatewayAdapter : IPaymentGateway
{
    private readonly MercadoPagoClient _client;

    public MercadoPagoGatewayAdapter(MercadoPagoClient client)
    {
        _client = client;
    }

    public string Name => "mercadopago";

    public Payment Create(CreatePaymentCommand command)
    {
        var payment = _client.Post(new MpPaymentPayload
        {
            TransactionAmount = (double)command.Amount,
            PaymentMethodId = ToMercadoPagoMethod(command.Method),
            PayerEmail = command.CustomerReference
        });

        return ToPayment(payment);
    }

    public Payment GetById(string externalId)
    {
        if (!long.TryParse(externalId, out var id))
        {
            throw new ArgumentException(
                $"Mercado Pago ids are numeric, received '{externalId}'.",
                nameof(externalId));
        }

        return ToPayment(_client.Get(id));
    }

    private static Payment ToPayment(MpPayment payment) =>
        new(
            ExternalId: payment.IdPagamento.ToString(),
            Status: ToPaymentStatus(payment.StatusPagamento),
            Amount: (decimal)payment.ValorTransacao,
            Method: ToPaymentMethod(payment.MeioPagamento));

    private static string ToMercadoPagoMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Pix => "pix",
        PaymentMethod.Boleto => "bolbradesco",
        PaymentMethod.CreditCard => "credit_card",
        _ => throw new NotSupportedException($"Mercado Pago does not support {method}.")
    };

    private static PaymentMethod ToPaymentMethod(string methodId) => methodId switch
    {
        "pix" => PaymentMethod.Pix,
        "bolbradesco" or "boleto" => PaymentMethod.Boleto,
        "credit_card" or "master" or "visa" => PaymentMethod.CreditCard,
        _ => throw new NotSupportedException($"Unknown Mercado Pago method: {methodId}.")
    };

    private static PaymentStatus ToPaymentStatus(string status) => status switch
    {
        "pending" or "in_process" or "authorized" => PaymentStatus.Pending,
        "approved" => PaymentStatus.Paid,
        "rejected" => PaymentStatus.Failed,
        "cancelled" => PaymentStatus.Cancelled,
        "refunded" or "charged_back" => PaymentStatus.Refunded,
        _ => throw new NotSupportedException($"Unmapped Mercado Pago status: {status}.")
    };
}
