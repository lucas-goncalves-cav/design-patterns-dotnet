using DesignPatterns.Behavioral.ChainOfResponsibility;
using DesignPatterns.Behavioral.ChainOfResponsibility.GoodExample;
using DesignPatterns.Behavioral.Observer;
using DesignPatterns.Behavioral.Observer.GoodExample;
using DesignPatterns.Behavioral.Strategy.GoodExample;
using DesignPatterns.RealWorld.OrderCheckout;
using FluentAssertions;
using PaymentMethod = DesignPatterns.Behavioral.Strategy.PaymentMethod;

namespace DesignPatterns.Tests.RealWorld;

/// <summary>
/// Four patterns working together on one operation. These tests exist to show
/// that each one stays in its own lane: the validation chain knows nothing
/// about payment methods, the payment strategy knows nothing about stock, and
/// neither knows what happens after the order is confirmed.
/// </summary>
public class CheckoutPipelineTests
{
    private static readonly Guid CustomerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private sealed record Harness(
        CheckoutPipeline Pipeline,
        FakeEmailSender Email,
        FakeStockLedger Stock,
        FakeAuditLog Audit);

    private static Harness Build(IEmailSender? email = null)
    {
        var customers = new FakeCustomerRepository();
        var emailSender = email as FakeEmailSender ?? new FakeEmailSender();
        var stock = new FakeStockLedger();
        var audit = new FakeAuditLog();

        var validator = OrderValidator.CreateDefault(
            customers,
            new FakeStockChecker(),
            new FakePaymentAuthorizer(),
            new FakeCouponService());

        var payments = new PaymentProcessor(
        [
            new PixPaymentStrategy(),
            new CreditCardPaymentStrategy(),
            new BoletoPaymentStrategy(),
            new BankTransferPaymentStrategy()
        ]);

        var publisher = new OrderConfirmedPublisher(
        [
            new AuditOrderConfirmedHandler(audit),
            new SendConfirmationEmailHandler(email ?? emailSender),
            new DecreaseStockHandler(stock)
        ]);

        return new Harness(new CheckoutPipeline(validator, payments, publisher), emailSender, stock, audit);
    }

    private static CheckoutRequest ValidRequest(
        PaymentMethod method = PaymentMethod.Pix,
        string? coupon = null,
        int installments = 1) =>
        new(
            CustomerId,
            [new OrderItem("SKU-1", 2, 100m), new OrderItem("SKU-2", 1, 50m)],
            method,
            "tok_valid",
            "customer@example.com",
            coupon,
            installments);

    [Fact]
    public void AValidCheckoutValidatesChargesAndAnnounces()
    {
        var harness = Build();

        var outcome = harness.Pipeline.Checkout(ValidRequest());

        outcome.Succeeded.Should().BeTrue();
        outcome.OrderId.Should().NotBeNull();
        outcome.Payment!.Fee.Should().Be(2.48m); // 250 * 0.0099
        harness.Email.Sent.Should().ContainSingle();
        harness.Stock.Movements.Should().HaveCount(2);
        harness.Audit.Entries.Should().ContainSingle();
        outcome.SideEffectFailures.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PaymentMethod.Pix)]
    [InlineData(PaymentMethod.Boleto)]
    [InlineData(PaymentMethod.BankTransfer)]
    public void TheChosenStrategyDecidesTheFee(PaymentMethod method)
    {
        var outcome = Build().Pipeline.Checkout(ValidRequest(method));

        outcome.Succeeded.Should().BeTrue();
        outcome.Payment!.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreditCardInstallmentsReachTheStrategy()
    {
        var single = Build().Pipeline.Checkout(ValidRequest(PaymentMethod.CreditCard));
        var installments = Build().Pipeline.Checkout(ValidRequest(PaymentMethod.CreditCard, installments: 6));

        installments.Payment!.Fee.Should().BeGreaterThan(single.Payment!.Fee);
        installments.Payment.Description.Should().Contain("6x");
    }

    /// <summary>
    /// The validation chain rejects before anything is charged or announced.
    /// </summary>
    [Fact]
    public void AnInvalidOrderIsRejectedBeforeChargingOrAnnouncing()
    {
        var harness = Build();
        var request = ValidRequest() with { Items = [] };

        var outcome = harness.Pipeline.Checkout(request);

        outcome.Succeeded.Should().BeFalse();
        outcome.RejectedStep.Should().Be("basket");
        outcome.Payment.Should().BeNull();
        harness.Email.Sent.Should().BeEmpty();
        harness.Stock.Movements.Should().BeEmpty();
    }

    [Fact]
    public void AnOutOfStockItemIsRejectedByTheChain()
    {
        var harness = Build();
        var request = ValidRequest() with { Items = [new OrderItem("SKU-OUT", 1, 100m)] };

        var outcome = harness.Pipeline.Checkout(request);

        outcome.RejectedStep.Should().Be("stock");
        harness.Stock.Movements.Should().BeEmpty();
    }

    [Fact]
    public void AValidCouponReducesTheChargedAmount()
    {
        var withoutCoupon = Build().Pipeline.Checkout(ValidRequest());
        var withCoupon = Build().Pipeline.Checkout(ValidRequest(coupon: "SAVE10"));

        withCoupon.Payment!.AmountCharged.Should().BeLessThan(withoutCoupon.Payment!.AmountCharged);
    }

    [Fact]
    public void AnInvalidCouponIsRejectedByTheChain()
    {
        var outcome = Build().Pipeline.Checkout(ValidRequest(coupon: "NOPE"));

        outcome.Succeeded.Should().BeFalse();
        outcome.RejectedStep.Should().Be("coupon");
    }

    /// <summary>
    /// The behaviour that makes the combination worth it: a failed welcome
    /// email does not undo a paid order, and the failure is still reported.
    /// </summary>
    [Fact]
    public void AFailedSideEffectDoesNotUndoAPaidOrder()
    {
        var harness = Build(new ThrowingEmailSender());

        var outcome = harness.Pipeline.Checkout(ValidRequest());

        outcome.Succeeded.Should().BeTrue();
        outcome.Payment.Should().NotBeNull();
        harness.Stock.Movements.Should().HaveCount(2);

        outcome.SideEffectFailures.Should().ContainSingle();
        outcome.SideEffectFailures.Single().HandlerName.Should().Be("email");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public bool Exists(Guid customerId) => customerId == CustomerId;

        public bool IsBlocked(Guid customerId) => false;

        public decimal CreditLimit(Guid customerId) => 100_000m;
    }

    private sealed class FakeStockChecker : IStockChecker
    {
        public int AvailableQuantity(string sku) => sku == "SKU-OUT" ? 0 : 100;
    }

    private sealed class FakePaymentAuthorizer : IPaymentAuthorizer
    {
        public bool Authorize(string paymentToken, decimal amount) => paymentToken != "tok_declined";
    }

    private sealed class FakeCouponService : ICouponService
    {
        public bool IsValid(string couponCode) => couponCode == "SAVE10";
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public void Send(string to, string subject, string body) => Sent.Add((to, subject, body));
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public void Send(string to, string subject, string body) =>
            throw new InvalidOperationException("SMTP unavailable.");
    }

    private sealed class FakeStockLedger : IStockLedger
    {
        public List<(string Sku, int Quantity)> Movements { get; } = [];

        public void Decrease(string sku, int quantity) => Movements.Add((sku, quantity));
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<string> Entries { get; } = [];

        public void Record(string message) => Entries.Add(message);
    }
}
