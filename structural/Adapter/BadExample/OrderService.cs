using DesignPatterns.Structural.Adapter.ExternalSdks;

namespace DesignPatterns.Structural.Adapter.BadExample;

/// <summary>
/// The provider SDKs are used directly, so their vocabulary spreads through
/// the application.
///
/// Notice what the business logic now has to know:
///
///   - Stripe counts cents, Asaas counts reais and Mercado Pago uses a double
///   - "RECEIVED", "succeeded" and "approved" all mean paid
///   - Stripe returns a string id, Mercado Pago returns a long
///
/// None of that is a business rule, and all of it is now business logic.
/// </summary>
public sealed class OrderService
{
    private readonly AsaasClient _asaas = new();
    private readonly StripeClient _stripe = new();
    private readonly MercadoPagoClient _mercadoPago = new();

    public string CreateCharge(string provider, decimal amount, string method, string customer)
    {
        switch (provider)
        {
            case "asaas":
                var asaas = _asaas.CreatePayment(new AsaasPaymentRequest
                {
                    Customer = customer,
                    Value = amount,
                    DueDate = DateTime.UtcNow.AddDays(3),
                    BillingType = method.ToUpperInvariant() == "PIX" ? "PIX" : "BOLETO"
                });
                return asaas.Id;

            case "stripe":
                // Multiplying by 100 here, and hoping nobody forgets it elsewhere.
                var stripe = _stripe.Create(new StripePaymentIntentOptions
                {
                    Amount = (long)(amount * 100),
                    Currency = "brl",
                    Customer = customer
                });
                return stripe.Id;

            case "mercadopago":
                var mp = _mercadoPago.Post(new MpPaymentPayload
                {
                    TransactionAmount = (double)amount,
                    PaymentMethodId = method.ToLowerInvariant(),
                    PayerEmail = customer
                });
                // A long turned into a string, losing the type on the way out.
                return mp.IdPagamento.ToString();

            default:
                throw new NotSupportedException(provider);
        }
    }

    /// <summary>
    /// The real cost of the bad version. Every status comparison in the
    /// application has to know every provider's spelling of "paid", and this
    /// method is copied wherever that question is asked.
    /// </summary>
    public bool IsPaid(string provider, string externalId)
    {
        switch (provider)
        {
            case "asaas":
                var asaas = _asaas.GetPayment(externalId);
                return asaas.Status is "RECEIVED" or "CONFIRMED";

            case "stripe":
                var stripe = _stripe.Retrieve(externalId);
                return stripe.Status == "succeeded";

            case "mercadopago":
                var mp = _mercadoPago.Get(long.Parse(externalId));
                return mp.StatusPagamento == "approved";

            default:
                throw new NotSupportedException(provider);
        }
    }
}
