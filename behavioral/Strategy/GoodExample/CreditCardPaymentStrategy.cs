namespace DesignPatterns.Behavioral.Strategy.GoodExample;

public sealed class CreditCardPaymentStrategy : IPaymentStrategy
{
    private const decimal BaseFeeRate = 0.0399m;
    private const decimal InstallmentFeeRate = 0.0199m;
    private const int MaxInstallments = 12;

    public PaymentMethod Method => PaymentMethod.CreditCard;

    public PaymentResult Process(PaymentRequest request)
    {
        if (request.Installments is < 1 or > MaxInstallments)
        {
            throw new ArgumentException(
                $"Credit card allows 1 to {MaxInstallments} installments.",
                nameof(request));
        }

        var fee = request.Amount * BaseFeeRate;

        if (request.Installments > 1)
        {
            fee += request.Amount * InstallmentFeeRate * request.Installments;
        }

        return new PaymentResult(
            Approved: true,
            AmountCharged: request.Amount + fee,
            Fee: decimal.Round(fee, 2),
            SettlesOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            Description: $"Credit card in {request.Installments}x, settled in 30 days");
    }
}
