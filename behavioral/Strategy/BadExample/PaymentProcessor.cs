namespace DesignPatterns.Behavioral.Strategy.BadExample;

/// <summary>
/// One method, one switch, every payment method. It works, and it is the most
/// common starting point in real codebases.
///
/// The problems show up on the fourth change, not the first:
///
///   - Adding a payment method means editing a class that already works,
///     so every existing method is retested to ship one new one
///   - The fee rules, the settlement rules and the validation rules for four
///     unrelated products are interleaved in one method
///   - Testing the boleto fee requires constructing the whole processor
///   - Two developers adding two payment methods conflict in the same lines
/// </summary>
public sealed class PaymentProcessor
{
    public PaymentResult Process(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.", nameof(request));
        }

        decimal fee;
        DateOnly settlesOn;
        string description;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        switch (request.Method)
        {
            case PaymentMethod.Pix:
                fee = request.Amount * 0.0099m;
                settlesOn = today;
                description = "PIX, settled instantly";
                break;

            case PaymentMethod.CreditCard:
                if (request.Installments is < 1 or > 12)
                {
                    throw new ArgumentException("Credit card allows 1 to 12 installments.", nameof(request));
                }

                fee = request.Amount * 0.0399m;

                if (request.Installments > 1)
                {
                    fee += request.Amount * 0.0199m * request.Installments;
                }

                settlesOn = today.AddDays(30);
                description = $"Credit card in {request.Installments}x, settled in 30 days";
                break;

            case PaymentMethod.Boleto:
                fee = 3.49m;
                settlesOn = today.AddDays(3);
                description = "Boleto, settled 3 days after payment";
                break;

            case PaymentMethod.BankTransfer:
                fee = 0m;
                settlesOn = today.AddDays(1);
                description = "Bank transfer, settled next business day";
                break;

            // Every new payment method adds a case here, and a matching case in
            // every other switch that grew alongside this one.
            default:
                throw new NotSupportedException($"Payment method {request.Method} is not supported.");
        }

        return new PaymentResult(
            Approved: true,
            AmountCharged: request.Amount + fee,
            Fee: decimal.Round(fee, 2),
            SettlesOn: settlesOn,
            Description: description);
    }
}
