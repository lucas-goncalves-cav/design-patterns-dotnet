namespace DesignPatterns.Behavioral.Strategy.GoodExample;

/// <summary>
/// The context. It knows the contract, never the implementations.
///
/// Adding a payment method means adding a class and registering it. This file
/// does not change, so it does not need retesting.
/// </summary>
public sealed class PaymentProcessor
{
    private readonly IReadOnlyDictionary<PaymentMethod, IPaymentStrategy> _strategies;

    public PaymentProcessor(IEnumerable<IPaymentStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(strategy => strategy.Method);
    }

    public PaymentResult Process(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.", nameof(request));
        }

        if (!_strategies.TryGetValue(request.Method, out var strategy))
        {
            throw new NotSupportedException($"Payment method {request.Method} is not supported.");
        }

        return strategy.Process(request);
    }
}
