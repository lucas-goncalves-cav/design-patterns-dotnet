using DesignPatterns.Behavioral.Strategy;
using DesignPatterns.Behavioral.Strategy.GoodExample;
using FluentAssertions;
using BadProcessor = DesignPatterns.Behavioral.Strategy.BadExample.PaymentProcessor;
using GoodProcessor = DesignPatterns.Behavioral.Strategy.GoodExample.PaymentProcessor;

namespace DesignPatterns.Tests.Behavioral;

/// <summary>
/// The refactor is only worth anything if behaviour is preserved. These tests
/// run both versions against the same inputs and assert they agree.
/// </summary>
public class StrategyTests
{
    private static GoodProcessor CreateGoodProcessor() =>
        new(
        [
            new PixPaymentStrategy(),
            new CreditCardPaymentStrategy(),
            new BoletoPaymentStrategy(),
            new BankTransferPaymentStrategy()
        ]);

    public static TheoryData<PaymentRequest> EquivalentRequests() =>
    [
        new PaymentRequest(PaymentMethod.Pix, 100m, "12345678900"),
        new PaymentRequest(PaymentMethod.Pix, 4_999.99m, "12345678900"),
        new PaymentRequest(PaymentMethod.CreditCard, 1_200m, "12345678900"),
        new PaymentRequest(PaymentMethod.CreditCard, 1_200m, "12345678900", Installments: 6),
        new PaymentRequest(PaymentMethod.CreditCard, 1_200m, "12345678900", Installments: 12),
        new PaymentRequest(PaymentMethod.Boleto, 250m, "12345678900"),
        new PaymentRequest(PaymentMethod.BankTransfer, 780.50m, "12345678900")
    ];

    [Theory]
    [MemberData(nameof(EquivalentRequests))]
    public void BothVersionsProduceTheSameResult(PaymentRequest request)
    {
        var bad = new BadProcessor().Process(request);
        var good = CreateGoodProcessor().Process(request);

        good.Should().BeEquivalentTo(bad);
    }

    [Fact]
    public void PixChargesAlmostOnePercentAndSettlesToday()
    {
        var result = CreateGoodProcessor().Process(new PaymentRequest(PaymentMethod.Pix, 1_000m, "12345678900"));

        result.Fee.Should().Be(9.90m);
        result.AmountCharged.Should().Be(1_009.90m);
        result.SettlesOn.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public void BoletoChargesAFlatFeeRegardlessOfAmount()
    {
        var processor = CreateGoodProcessor();

        var small = processor.Process(new PaymentRequest(PaymentMethod.Boleto, 10m, "12345678900"));
        var large = processor.Process(new PaymentRequest(PaymentMethod.Boleto, 10_000m, "12345678900"));

        small.Fee.Should().Be(3.49m);
        large.Fee.Should().Be(3.49m);
    }

    [Fact]
    public void BankTransferIsFree()
    {
        var result = CreateGoodProcessor()
            .Process(new PaymentRequest(PaymentMethod.BankTransfer, 500m, "12345678900"));

        result.Fee.Should().Be(0m);
        result.AmountCharged.Should().Be(500m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void CreditCardRejectsInstallmentsOutsideTheAllowedRange(int installments)
    {
        var request = new PaymentRequest(PaymentMethod.CreditCard, 100m, "12345678900", installments);

        var act = () => CreateGoodProcessor().Process(request);

        act.Should().Throw<ArgumentException>().WithMessage("Credit card allows 1 to 12 installments.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AmountMustBePositive(decimal amount)
    {
        var request = new PaymentRequest(PaymentMethod.Pix, amount, "12345678900");

        var act = () => CreateGoodProcessor().Process(request);

        act.Should().Throw<ArgumentException>().WithMessage("Amount must be greater than zero.*");
    }

    [Fact]
    public void AnUnregisteredMethodIsRejectedInsteadOfSilentlyIgnored()
    {
        var processor = new GoodProcessor([new PixPaymentStrategy()]);
        var request = new PaymentRequest(PaymentMethod.Boleto, 100m, "12345678900");

        var act = () => processor.Process(request);

        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// The point of the pattern: a new payment method is a new class, and the
    /// processor is not touched.
    /// </summary>
    [Fact]
    public void ANewPaymentMethodRequiresNoChangeToTheProcessor()
    {
        var processor = new GoodProcessor([new PixPaymentStrategy(), new CryptoPaymentStrategy()]);

        var result = processor.Process(new PaymentRequest(PaymentMethod.BankTransfer, 1_000m, "12345678900"));

        result.Fee.Should().Be(15m);
        result.Description.Should().Contain("Crypto");
    }

    private sealed class CryptoPaymentStrategy : IPaymentStrategy
    {
        // Reusing an existing enum value keeps the example short. In a real
        // codebase this would be a new member of PaymentMethod.
        public PaymentMethod Method => PaymentMethod.BankTransfer;

        public PaymentResult Process(PaymentRequest request) =>
            new(
                Approved: true,
                AmountCharged: request.Amount + 15m,
                Fee: 15m,
                SettlesOn: DateOnly.FromDateTime(DateTime.UtcNow),
                Description: "Crypto, settled after network confirmation");
    }
}
