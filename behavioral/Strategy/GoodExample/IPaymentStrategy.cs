namespace DesignPatterns.Behavioral.Strategy.GoodExample;

/// <summary>
/// One payment method, one implementation. The contract is what every method
/// has in common; everything else stays inside the implementation.
/// </summary>
public interface IPaymentStrategy
{
    PaymentMethod Method { get; }

    PaymentResult Process(PaymentRequest request);
}
