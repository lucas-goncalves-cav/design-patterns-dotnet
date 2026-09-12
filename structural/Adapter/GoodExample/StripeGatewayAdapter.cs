using DesignPatterns.Structural.Adapter.ExternalSdks;

namespace DesignPatterns.Structural.Adapter.GoodExample;

public sealed class StripeGatewayAdapter : IPaymentGateway
{
    private const int CentsPerUnit = 100;

    private readonly StripeClient _client;

    public StripeGatewayAdapter(StripeClient client)
    {
        _client = client;
    }

    public string Name => "stripe";

    public Payment Create(CreatePaymentCommand command)
    {
        if (command.Method is not PaymentMethod.CreditCard and not PaymentMethod.Pix)
        {
            throw new NotSupportedException($"Stripe does not support {command.Method} in this integration.");
        }

        var intent = _client.Create(new StripePaymentIntentOptions
        {
            Amount = ToCents(command.Amount),
            Currency = "brl",
            Customer = command.CustomerReference
        });

        return ToPayment(intent, command.Method);
    }

    public Payment GetById(string externalId) =>
        ToPayment(_client.Retrieve(externalId), PaymentMethod.CreditCard);

    private static Payment ToPayment(StripePaymentIntent intent, PaymentMethod method) =>
        new(
            ExternalId: intent.Id,
            Status: ToPaymentStatus(intent.Status),
            Amount: FromCents(intent.Amount),
            Method: method);

    /// <summary>
    /// The cents conversion lives here and nowhere else. In the bad example it
    /// is an inline multiplication that someone eventually forgets.
    /// </summary>
    private static long ToCents(decimal amount) => (long)decimal.Round(amount * CentsPerUnit, 0);

    private static decimal FromCents(long cents) => cents / (decimal)CentsPerUnit;

    private static PaymentStatus ToPaymentStatus(string status) => status switch
    {
        "requires_payment_method" or "requires_confirmation" or "requires_action" or "processing"
            => PaymentStatus.Pending,
        "succeeded" => PaymentStatus.Paid,
        "canceled" => PaymentStatus.Cancelled,
        _ => throw new NotSupportedException($"Unmapped Stripe status: {status}.")
    };
}
