namespace DesignPatterns.Creational.Factory.GoodExample;

/// <summary>
/// The service asks for a gateway and charges. It does not know which
/// providers exist, how they are configured, or what their constructors
/// look like.
/// </summary>
public sealed class CheckoutService
{
    private readonly IPaymentGatewayFactory _factory;

    public CheckoutService(IPaymentGatewayFactory factory)
    {
        _factory = factory;
    }

    public ChargeResponse Checkout(ChargeRequest request) =>
        _factory.CreateDefault().CreateCharge(request);

    /// <summary>
    /// Charging through a specific provider, for example when a customer has a
    /// saved card with one of them, costs one extra argument.
    /// </summary>
    public ChargeResponse CheckoutWith(GatewayProvider provider, ChargeRequest request) =>
        _factory.Create(provider).CreateCharge(request);
}
