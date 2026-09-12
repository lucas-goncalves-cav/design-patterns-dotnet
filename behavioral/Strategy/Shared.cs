namespace DesignPatterns.Behavioral.Strategy;

public enum PaymentMethod
{
    Pix,
    CreditCard,
    Boleto,
    BankTransfer
}

public sealed record PaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    string CustomerDocument,
    int Installments = 1);

public sealed record PaymentResult(
    bool Approved,
    decimal AmountCharged,
    decimal Fee,
    DateOnly SettlesOn,
    string Description);
