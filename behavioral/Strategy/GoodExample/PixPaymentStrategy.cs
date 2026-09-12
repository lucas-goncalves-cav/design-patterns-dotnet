namespace DesignPatterns.Behavioral.Strategy.GoodExample;

public sealed class PixPaymentStrategy : IPaymentStrategy
{
    private const decimal FeeRate = 0.0099m;

    public PaymentMethod Method => PaymentMethod.Pix;

    public PaymentResult Process(PaymentRequest request)
    {
        var fee = request.Amount * FeeRate;

        return new PaymentResult(
            Approved: true,
            AmountCharged: request.Amount + fee,
            Fee: decimal.Round(fee, 2),
            SettlesOn: DateOnly.FromDateTime(DateTime.UtcNow),
            Description: "PIX, settled instantly");
    }
}
