using DesignPatterns.Behavioral.ChainOfResponsibility;
using DesignPatterns.Behavioral.ChainOfResponsibility.GoodExample;
using FluentAssertions;
using BadValidator = DesignPatterns.Behavioral.ChainOfResponsibility.BadExample.OrderValidator;
using GoodValidator = DesignPatterns.Behavioral.ChainOfResponsibility.GoodExample.OrderValidator;

namespace DesignPatterns.Tests.Behavioral;

public class ChainOfResponsibilityTests
{
    private static readonly Guid CustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static OrderRequest ValidOrder(string? coupon = null) =>
        new(
            CustomerId,
            [new OrderItem("SKU-1", 2, 50m), new OrderItem("SKU-2", 1, 100m)],
            "tok_valid",
            coupon);

    [Theory]
    [InlineData(null)]
    [InlineData("SAVE10")]
    public void BothVersionsAcceptTheSameValidOrder(string? coupon)
    {
        var request = ValidOrder(coupon);

        var bad = BuildBad().Validate(request);
        var good = BuildGood().Validate(request);

        bad.Succeeded.Should().BeTrue();
        good.Should().BeEquivalentTo(bad);
    }

    public static TheoryData<OrderRequest, string> RejectedOrders() =>
        new()
        {
            { new OrderRequest(CustomerId, [], "tok_valid"), "basket" },
            { new OrderRequest(CustomerId, [new OrderItem("SKU-1", 0, 50m)], "tok_valid"), "basket" },
            { new OrderRequest(Guid.NewGuid(), [new OrderItem("SKU-1", 1, 50m)], "tok_valid"), "customer" },
            { new OrderRequest(CustomerId, [new OrderItem("SKU-OUT", 1, 50m)], "tok_valid"), "stock" },
            { new OrderRequest(CustomerId, [new OrderItem("SKU-1", 1, 50m)], "tok_valid", "BADCOUPON"), "coupon" },
            { new OrderRequest(CustomerId, [new OrderItem("SKU-1", 100, 500m)], "tok_valid"), "credit" },
            { new OrderRequest(CustomerId, [new OrderItem("SKU-1", 1, 50m)], "tok_declined"), "payment" }
        };

    /// <summary>
    /// Every rejection path agrees between the two versions, including which
    /// step rejected and why.
    /// </summary>
    [Theory]
    [MemberData(nameof(RejectedOrders))]
    public void BothVersionsRejectForTheSameReason(OrderRequest request, string expectedStep)
    {
        var bad = BuildBad().Validate(request);
        var good = BuildGood().Validate(request);

        bad.Succeeded.Should().BeFalse();
        bad.FailedStep.Should().Be(expectedStep);
        good.Should().BeEquivalentTo(bad);
    }

    /// <summary>
    /// A cheap check rejecting the order means the expensive collaborators are
    /// never called. This is the behaviour the nesting in the bad version made
    /// hard to see.
    /// </summary>
    [Fact]
    public void ShortCircuitingStopsBeforeTheExpensiveSteps()
    {
        var payment = new CountingPaymentAuthorizer();
        var stock = new CountingStockChecker();

        var validator = new GoodValidator(
        [
            new BasketHandler(),
            new StockHandler(stock),
            new PaymentHandler(payment)
        ]);

        var result = validator.Validate(new OrderRequest(CustomerId, [], "tok_valid"));

        result.FailedStep.Should().Be("basket");
        stock.Calls.Should().Be(0);
        payment.Calls.Should().Be(0);
    }

    /// <summary>
    /// The coupon handler writes to the context, so the credit check sees the
    /// discounted total. Without the coupon this order would be rejected.
    /// </summary>
    [Fact]
    public void AValidCouponLowersTheTotalSeenByLaterSteps()
    {
        // 1,000 against a credit limit of 950 fails, 900 after the discount passes.
        var items = new[] { new OrderItem("SKU-1", 10, 100m) };

        var withoutCoupon = BuildGood().Validate(new OrderRequest(CustomerId, items, "tok_valid"));
        var withCoupon = BuildGood().Validate(new OrderRequest(CustomerId, items, "tok_valid", "SAVE10"));

        withoutCoupon.Succeeded.Should().BeFalse();
        withoutCoupon.FailedStep.Should().Be("credit");
        withCoupon.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void PaymentIsAuthorizedForTheDiscountedTotal()
    {
        var payment = new RecordingPaymentAuthorizer();

        var validator = new GoodValidator(
        [
            new CouponHandler(new FakeCouponService()),
            new PaymentHandler(payment)
        ]);

        validator.Validate(new OrderRequest(CustomerId, [new OrderItem("SKU-1", 1, 200m)], "tok_valid", "SAVE10"));

        payment.LastAmount.Should().Be(180m);
    }

    /// <summary>
    /// Reordering the pipeline is reordering a list, and the effect is visible.
    /// </summary>
    [Fact]
    public void ReorderingTheChainChangesWhichStepRejectsFirst()
    {
        var request = new OrderRequest(CustomerId, [new OrderItem("SKU-OUT", 1, 50m)], "tok_declined");

        var stockFirst = new GoodValidator(
        [
            new StockHandler(new FakeStockChecker()),
            new PaymentHandler(new FakePaymentAuthorizer())
        ]);

        var paymentFirst = new GoodValidator(
        [
            new PaymentHandler(new FakePaymentAuthorizer()),
            new StockHandler(new FakeStockChecker())
        ]);

        stockFirst.Validate(request).FailedStep.Should().Be("stock");
        paymentFirst.Validate(request).FailedStep.Should().Be("payment");
    }

    [Fact]
    public void ASingleHandlerChainWorks()
    {
        var validator = new GoodValidator([new BasketHandler()]);

        validator.Validate(ValidOrder()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void AnEmptyChainIsRejectedAtConstructionTime()
    {
        var act = () => new GoodValidator([]);

        act.Should().Throw<ArgumentException>().WithMessage("At least one handler is required.*");
    }

    // -------------------------------------------------------------------------
    // Builders and test doubles
    // -------------------------------------------------------------------------

    private static BadValidator BuildBad() =>
        new(new FakeCustomerRepository(), new FakeStockChecker(), new FakePaymentAuthorizer(), new FakeCouponService());

    private static GoodValidator BuildGood() =>
        GoodValidator.CreateDefault(
            new FakeCustomerRepository(),
            new FakeStockChecker(),
            new FakePaymentAuthorizer(),
            new FakeCouponService());

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public bool Exists(Guid customerId) => customerId == CustomerId;

        public bool IsBlocked(Guid customerId) => false;

        public decimal CreditLimit(Guid customerId) => 950m;
    }

    private sealed class FakeStockChecker : IStockChecker
    {
        public int AvailableQuantity(string sku) => sku == "SKU-OUT" ? 0 : 100;
    }

    private sealed class CountingStockChecker : IStockChecker
    {
        public int Calls { get; private set; }

        public int AvailableQuantity(string sku)
        {
            Calls++;

            return 100;
        }
    }

    private sealed class FakePaymentAuthorizer : IPaymentAuthorizer
    {
        public bool Authorize(string paymentToken, decimal amount) => paymentToken != "tok_declined";
    }

    private sealed class CountingPaymentAuthorizer : IPaymentAuthorizer
    {
        public int Calls { get; private set; }

        public bool Authorize(string paymentToken, decimal amount)
        {
            Calls++;

            return true;
        }
    }

    private sealed class RecordingPaymentAuthorizer : IPaymentAuthorizer
    {
        public decimal LastAmount { get; private set; }

        public bool Authorize(string paymentToken, decimal amount)
        {
            LastAmount = amount;

            return true;
        }
    }

    private sealed class FakeCouponService : ICouponService
    {
        public bool IsValid(string couponCode) => couponCode == "SAVE10";
    }
}
