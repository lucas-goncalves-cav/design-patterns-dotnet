namespace DesignPatterns.Behavioral.Strategy.GoodExample;

public sealed class BankTransferPaymentStrategy : IPaymentStrategy
{
    public PaymentMethod Method => PaymentMethod.BankTransfer;

    public PaymentResult Process(PaymentRequest request) =>
        new(
            Approved: true,
            AmountCharged: request.Amount,
            Fee: 0m,
            SettlesOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            Description: "Bank transfer, settled next business day");
}
