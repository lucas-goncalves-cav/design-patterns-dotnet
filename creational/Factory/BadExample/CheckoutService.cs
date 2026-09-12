namespace DesignPatterns.Creational.Factory.BadExample;

/// <summary>
/// The service builds its own gateway, so it knows every provider, every
/// constructor signature and where the configuration lives.
///
/// The cost:
///
///   - The service cannot be unit tested without real configuration, because
///     it constructs the gateway itself
///   - Adding a provider means editing the checkout logic
///   - Every other service that charges a customer repeats this switch
///   - The service depends on details it has no business knowing, such as
///     which providers need a sandbox flag
/// </summary>
public sealed class CheckoutService
{
    private readonly GatewaySettings _settings;

    public CheckoutService(GatewaySettings settings)
    {
        _settings = settings;
    }

    public ChargeResponse Checkout(ChargeRequest request)
    {
        IPaymentGateway gateway;

        switch (_settings.Provider)
        {
            case GatewayProvider.Asaas:
                gateway = new AsaasGateway(_settings.ApiKey, _settings.BaseUrl, _settings.Sandbox);
                break;

            case GatewayProvider.MercadoPago:
                gateway = new MercadoPagoGateway(_settings.ApiKey, _settings.BaseUrl);
                break;

            case GatewayProvider.Stripe:
                gateway = new StripeGateway(_settings.ApiKey);
                break;

            default:
                throw new NotSupportedException($"Provider {_settings.Provider} is not supported.");
        }

        return gateway.CreateCharge(request);
    }
}

internal sealed class AsaasGateway : IPaymentGateway
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly bool _sandbox;

    public AsaasGateway(string apiKey, string baseUrl, bool sandbox)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl;
        _sandbox = sandbox;
    }

    public GatewayProvider Provider => GatewayProvider.Asaas;

    public ChargeResponse CreateCharge(ChargeRequest request) =>
        new($"asaas_{Guid.NewGuid():N}", "PENDING", request.Amount);
}

internal sealed class MercadoPagoGateway : IPaymentGateway
{
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public MercadoPagoGateway(string apiKey, string baseUrl)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl;
    }

    public GatewayProvider Provider => GatewayProvider.MercadoPago;

    public ChargeResponse CreateCharge(ChargeRequest request) =>
        new($"mp_{Guid.NewGuid():N}", "pending", request.Amount);
}

internal sealed class StripeGateway : IPaymentGateway
{
    private readonly string _apiKey;

    public StripeGateway(string apiKey)
    {
        _apiKey = apiKey;
    }

    public GatewayProvider Provider => GatewayProvider.Stripe;

    public ChargeResponse CreateCharge(ChargeRequest request) =>
        new($"ch_{Guid.NewGuid():N}", "requires_payment_method", request.Amount);
}
