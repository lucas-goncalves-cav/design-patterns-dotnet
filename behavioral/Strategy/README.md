# Strategy

## The problem it solves

A single operation has several interchangeable algorithms, and which one runs is decided at runtime.

Without the pattern that decision lives in a `switch`, and every new algorithm means editing code that already works.

## The scenario

A checkout charges customers through PIX, credit card, boleto or bank transfer. Each has different fees, different
settlement times and different validation rules.

## Bad example

[`BadExample/PaymentProcessor.cs`](BadExample/PaymentProcessor.cs)

```csharp
switch (request.Method)
{
    case PaymentMethod.Pix:
        fee = request.Amount * 0.0099m;
        settlesOn = today;
        break;

    case PaymentMethod.CreditCard:
        if (request.Installments is < 1 or > 12) { throw ... }
        fee = request.Amount * 0.0399m;
        if (request.Installments > 1) { fee += ... }
        settlesOn = today.AddDays(30);
        break;

    // and so on
}
```

This is not stupid code. It is the honest first version, and for two payment methods it is the right amount of
structure. It stops being right around the fourth one:

- Adding a payment method means editing a class that currently works, so everything in it gets retested to ship one
  new feature
- Fee rules, settlement rules and validation for four unrelated products sit interleaved in one method
- Testing the boleto fee means constructing the whole processor
- Two developers adding two payment methods conflict in the same lines
- The same `switch` gets copied into the refund path, the reporting path and the reconciliation path, and they drift

## Good example

[`GoodExample/`](GoodExample/)

One interface, one implementation per method:

```csharp
public interface IPaymentStrategy
{
    PaymentMethod Method { get; }
    PaymentResult Process(PaymentRequest request);
}
```

The context resolves the right one and knows nothing about any of them:

```csharp
public sealed class PaymentProcessor
{
    private readonly IReadOnlyDictionary<PaymentMethod, IPaymentStrategy> _strategies;

    public PaymentProcessor(IEnumerable<IPaymentStrategy> strategies) =>
        _strategies = strategies.ToDictionary(strategy => strategy.Method);

    public PaymentResult Process(PaymentRequest request)
    {
        if (!_strategies.TryGetValue(request.Method, out var strategy))
        {
            throw new NotSupportedException($"Payment method {request.Method} is not supported.");
        }

        return strategy.Process(request);
    }
}
```

Registration in `Program.cs`:

```csharp
services.AddScoped<IPaymentStrategy, PixPaymentStrategy>();
services.AddScoped<IPaymentStrategy, CreditCardPaymentStrategy>();
services.AddScoped<IPaymentStrategy, BoletoPaymentStrategy>();
services.AddScoped<IPaymentStrategy, BankTransferPaymentStrategy>();
```

Injecting `IEnumerable<IPaymentStrategy>` gives the processor every registration, which is what removes the last
reference to a concrete type.

## What actually changed

| | Bad | Good |
| --- | --- | --- |
| Adding a method | Edit a working class | Add a class, add one registration line |
| Testing one method | Build the whole processor | Instantiate one strategy |
| Merge conflicts | Same lines, every time | Different files |
| Unsupported method | Silent fall through or a `default` throw | Explicit, and the container proves what is registered |

The test suite runs both versions against the same inputs and asserts they agree, so the refactor is provably
behaviour preserving: [`StrategyTests.cs`](../../tests/DesignPatterns.Tests/Behavioral/StrategyTests.cs)

## When to use it

- Several interchangeable algorithms for the same operation
- The choice is made at runtime, from configuration, user input or data
- You expect the set of algorithms to grow
- You want to test each algorithm in isolation

## When not to use it

- **Two cases that will stay two cases.** An `if` is clearer than an interface, two classes and a registration.
- **The branches are one line each.** A dictionary of `Func<T, TResult>` gets you there with less ceremony.
- **The cases are not actually interchangeable.** If each branch takes different inputs and returns different types,
  they do not share a contract and forcing one produces a bloated interface where most implementations throw
  `NotSupportedException`.
- **The set is closed and exhaustive.** For something genuinely fixed like the four arithmetic operators, a `switch`
  expression is fine and the compiler can check exhaustiveness for you.

## Related patterns

| Pattern | Difference |
| --- | --- |
| [Factory](../../creational/Factory/) | Factory decides which object to create, Strategy decides which algorithm runs. They are often used together. |
| [Chain of Responsibility](../ChainOfResponsibility/) | Chain passes a request along until someone handles it. Strategy picks exactly one handler up front. |
| [State](https://refactoring.guru/design-patterns/state) | Same structure, different intent. State transitions between implementations itself; Strategy is chosen from outside. |

## The C# specific note

`switch` expressions and pattern matching have made the "bad" version more tolerable than it used to be. A modern
version of the same code reads reasonably well:

```csharp
var fee = request.Method switch
{
    PaymentMethod.Pix        => request.Amount * 0.0099m,
    PaymentMethod.CreditCard => CalculateCardFee(request),
    PaymentMethod.Boleto     => 3.49m,
    _ => throw new NotSupportedException()
};
```

That is genuinely fine when the branches stay one line. The moment a branch needs its own validation, its own
settlement logic and its own dependencies, extract it. The signal is not the `switch` itself, it is the second
`switch` on the same enum appearing somewhere else in the codebase.
