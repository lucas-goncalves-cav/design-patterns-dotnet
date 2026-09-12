namespace DesignPatterns.Creational.Factory.GoodExample;

public sealed class AsaasGateway : IPaymentGateway
{
    private readonly GatewaySettings _settings;

    public AsaasGateway(GatewaySettings settings)
    {
        _settings = settings;
    }

    public GatewayProvider Provider => GatewayProvider.Asaas;

    public ChargeResponse CreateCharge(ChargeRequest request) =>
        new($"asaas_{Guid.NewGuid():N}", "PENDING", request.Amount);
}

public sealed class MercadoPagoGateway : IPaymentGateway
{
    private readonly GatewaySettings _settings;

    public MercadoPagoGateway(GatewaySettings settings)
    {
        _settings = settings;
    }

    public GatewayProvider Provider => GatewayProvider.MercadoPago;

    public ChargeResponse CreateCharge(ChargeRequest request) =>
        new($"mp_{Guid.NewGuid():N}", "pending", request.Amount);
}

public sealed class StripeGateway : IPaymentGateway
{
    private readonly GatewaySettings _settings;

    public StripeGateway(GatewaySettings settings)
    {
        _settings = settings;
    }

    public GatewayProvider Provider => GatewayProvider.Stripe;

    public ChargeResponse CreateCharge(ChargeRequest request) =>
        new($"ch_{Guid.NewGuid():N}", "requires_payment_method", request.Amount);
}
