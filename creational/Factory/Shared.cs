namespace DesignPatterns.Creational.Factory;

public enum GatewayProvider
{
    Asaas,
    MercadoPago,
    Stripe
}

public sealed record GatewaySettings(
    GatewayProvider Provider,
    string ApiKey,
    string BaseUrl,
    bool Sandbox);

public sealed record ChargeRequest(decimal Amount, string CustomerId, string Description);

public sealed record ChargeResponse(string ProviderChargeId, string Status, decimal Amount);

public interface IPaymentGateway
{
    GatewayProvider Provider { get; }

    ChargeResponse CreateCharge(ChargeRequest request);
}
