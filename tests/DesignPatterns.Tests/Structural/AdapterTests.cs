using DesignPatterns.Structural.Adapter;
using DesignPatterns.Structural.Adapter.ExternalSdks;
using DesignPatterns.Structural.Adapter.GoodExample;
using FluentAssertions;

namespace DesignPatterns.Tests.Structural;

public class AdapterTests
{
    private static readonly CreatePaymentCommand PixCommand = new(
        Amount: 150.75m,
        Method: PaymentMethod.Pix,
        CustomerReference: "customer@example.com",
        DueDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3));

    public static TheoryData<IPaymentGateway, string> Gateways() =>
        new()
        {
            { new AsaasGatewayAdapter(new AsaasClient()), "asaas" },
            { new StripeGatewayAdapter(new StripeClient()), "stripe" },
            { new MercadoPagoGatewayAdapter(new MercadoPagoClient()), "mercadopago" }
        };

    /// <summary>
    /// The point of the pattern: three incompatible SDKs, one contract, and
    /// the caller cannot tell them apart.
    /// </summary>
    [Theory]
    [MemberData(nameof(Gateways))]
    public void EveryGatewayReturnsTheSameShape(IPaymentGateway gateway, string expectedName)
    {
        var payment = gateway.Create(PixCommand);

        gateway.Name.Should().Be(expectedName);
        payment.ExternalId.Should().NotBeNullOrWhiteSpace();
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.Amount.Should().Be(150.75m);
        payment.Method.Should().Be(PaymentMethod.Pix);
    }

    [Theory]
    [MemberData(nameof(Gateways))]
    public void OrderServiceWorksWithAnyGatewayWithoutKnowingWhichOne(IPaymentGateway gateway, string _)
    {
        var service = new OrderService(gateway);

        var payment = service.CreateCharge(PixCommand);

        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    /// <summary>
    /// Stripe counts cents. The adapter is the only place that knows, and it
    /// converts in both directions without losing value.
    /// </summary>
    [Fact]
    public void StripeAmountsSurviveTheRoundTripThroughCents()
    {
        var gateway = new StripeGatewayAdapter(new StripeClient());
        var command = PixCommand with { Amount = 1_234.56m };

        var payment = gateway.Create(command);

        payment.Amount.Should().Be(1_234.56m);
    }

    [Fact]
    public void ProviderStatusVocabulariesAllMapToPaid()
    {
        // "RECEIVED", "succeeded" and "approved" are the same fact in three
        // languages. Each stub returns its provider's spelling.
        new AsaasGatewayAdapter(new AsaasClient()).GetById("pay_1").Status.Should().Be(PaymentStatus.Paid);
        new StripeGatewayAdapter(new StripeClient()).GetById("pi_1").Status.Should().Be(PaymentStatus.Paid);
        new MercadoPagoGatewayAdapter(new MercadoPagoClient()).GetById("12345").Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void MercadoPagoRejectsANonNumericIdInsteadOfThrowingAFormatException()
    {
        var gateway = new MercadoPagoGatewayAdapter(new MercadoPagoClient());

        var act = () => gateway.GetById("pay_not_numeric");

        act.Should().Throw<ArgumentException>().WithMessage("*numeric*");
    }

    [Fact]
    public void AGatewayRejectsAMethodItDoesNotSupport()
    {
        var gateway = new StripeGatewayAdapter(new StripeClient());
        var command = PixCommand with { Method = PaymentMethod.Boleto };

        var act = () => gateway.Create(command);

        act.Should().Throw<NotSupportedException>().WithMessage("*Boleto*");
    }

    /// <summary>
    /// Swapping providers is a registration change. No business code moves.
    /// </summary>
    [Fact]
    public void SwappingTheProviderDoesNotChangeTheCallingCode()
    {
        var withAsaas = new OrderService(new AsaasGatewayAdapter(new AsaasClient()));
        var withStripe = new OrderService(new StripeGatewayAdapter(new StripeClient()));

        var fromAsaas = withAsaas.CreateCharge(PixCommand);
        var fromStripe = withStripe.CreateCharge(PixCommand);

        fromAsaas.Status.Should().Be(fromStripe.Status);
        fromAsaas.Amount.Should().Be(fromStripe.Amount);
        fromAsaas.Method.Should().Be(fromStripe.Method);
    }
}
