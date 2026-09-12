namespace DesignPatterns.Creational.Factory.GoodExample;

/// <summary>
/// Resolves the gateway registered for a provider.
///
/// Taking the implementations as a collection rather than newing them up keeps
/// construction in the DI container, so a gateway is free to take whatever
/// dependencies it needs without this class ever learning about them.
/// </summary>
public sealed class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IReadOnlyDictionary<GatewayProvider, IPaymentGateway> _gateways;
    private readonly GatewayProvider _defaultProvider;

    public PaymentGatewayFactory(IEnumerable<IPaymentGateway> gateways, GatewaySettings settings)
    {
        _gateways = gateways.ToDictionary(gateway => gateway.Provider);
        _defaultProvider = settings.Provider;
    }

    public IPaymentGateway Create(GatewayProvider provider)
    {
        if (!_gateways.TryGetValue(provider, out var gateway))
        {
            throw new NotSupportedException(
                $"Provider {provider} is not registered. Registered: {string.Join(", ", _gateways.Keys)}.");
        }

        return gateway;
    }

    public IPaymentGateway CreateDefault() => Create(_defaultProvider);
}
