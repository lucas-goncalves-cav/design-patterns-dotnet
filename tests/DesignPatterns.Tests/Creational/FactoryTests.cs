using DesignPatterns.Creational.Factory;
using DesignPatterns.Creational.Factory.GoodExample;
using FluentAssertions;
using BadCheckout = DesignPatterns.Creational.Factory.BadExample.CheckoutService;
using GoodCheckout = DesignPatterns.Creational.Factory.GoodExample.CheckoutService;

namespace DesignPatterns.Tests.Creational;

public class FactoryTests
{
    private static GatewaySettings Settings(GatewayProvider provider) =>
        new(provider, "your_api_key_here", "https://sandbox.example.com", Sandbox: true);

    private static PaymentGatewayFactory CreateFactory(GatewayProvider defaultProvider)
    {
        var settings = Settings(defaultProvider);

        return new PaymentGatewayFactory(
            [
                new AsaasGateway(settings),
                new MercadoPagoGateway(settings),
                new StripeGateway(settings)
            ],
            settings);
    }

    [Theory]
    [InlineData(GatewayProvider.Asaas, "asaas_")]
    [InlineData(GatewayProvider.MercadoPago, "mp_")]
    [InlineData(GatewayProvider.Stripe, "ch_")]
    public void BothVersionsRouteToTheSameProvider(GatewayProvider provider, string expectedPrefix)
    {
        var request = new ChargeRequest(100m, "customer-1", "Order 1234");

        var bad = new BadCheckout(Settings(provider)).Checkout(request);
        var good = new GoodCheckout(CreateFactory(provider)).Checkout(request);

        bad.ProviderChargeId.Should().StartWith(expectedPrefix);
        good.ProviderChargeId.Should().StartWith(expectedPrefix);
        good.Amount.Should().Be(bad.Amount);
        good.Status.Should().Be(bad.Status);
    }

    [Fact]
    public void TheFactoryCanOverrideTheDefaultProviderPerCall()
    {
        var checkout = new GoodCheckout(CreateFactory(GatewayProvider.Asaas));
        var request = new ChargeRequest(250m, "customer-2", "Order 5678");

        var byDefault = checkout.Checkout(request);
        var overridden = checkout.CheckoutWith(GatewayProvider.Stripe, request);

        byDefault.ProviderChargeId.Should().StartWith("asaas_");
        overridden.ProviderChargeId.Should().StartWith("ch_");
    }

    [Fact]
    public void AnUnregisteredProviderFailsWithAUsefulMessage()
    {
        var settings = Settings(GatewayProvider.Asaas);
        var factory = new PaymentGatewayFactory([new AsaasGateway(settings)], settings);

        var act = () => factory.Create(GatewayProvider.Stripe);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Stripe is not registered*Asaas*");
    }

    /// <summary>
    /// The reason the refactor is worth doing: the service can now be tested
    /// without any real provider, because it never constructs one.
    /// </summary>
    [Fact]
    public void TheServiceCanBeTestedWithAFakeGateway()
    {
        var settings = Settings(GatewayProvider.Asaas);
        var fake = new RecordingGateway();
        var checkout = new GoodCheckout(new PaymentGatewayFactory([fake], settings));

        var response = checkout.Checkout(new ChargeRequest(99.90m, "customer-3", "Order 9012"));

        fake.ReceivedRequests.Should().ContainSingle();
        fake.ReceivedRequests[0].Amount.Should().Be(99.90m);
        response.Status.Should().Be("FAKE_APPROVED");
    }

    private sealed class RecordingGateway : IPaymentGateway
    {
        public List<ChargeRequest> ReceivedRequests { get; } = [];

        public GatewayProvider Provider => GatewayProvider.Asaas;

        public ChargeResponse CreateCharge(ChargeRequest request)
        {
            ReceivedRequests.Add(request);

            return new ChargeResponse("fake_1", "FAKE_APPROVED", request.Amount);
        }
    }
}
