namespace DesignPatterns.Behavioral.Strategy.GoodExample;

public sealed class BoletoPaymentStrategy : IPaymentStrategy
{
    private const decimal FlatFee = 3.49m;

    public PaymentMethod Method => PaymentMethod.Boleto;

    public PaymentResult Process(PaymentRequest request) =>
        new(
            Approved: true,
            AmountCharged: request.Amount + FlatFee,
            Fee: FlatFee,
            SettlesOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3),
            Description: "Boleto, settled 3 days after payment");
}
