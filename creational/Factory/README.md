# Factory

## The problem it solves

A class needs an object whose concrete type is decided at runtime. Constructing it inline couples that class to every
possible implementation and to how each one is configured.

## The scenario

A checkout charges through Asaas, Mercado Pago or Stripe. Which one is configuration, and each has a different
constructor.

## Bad example

[`BadExample/CheckoutService.cs`](BadExample/CheckoutService.cs)

```csharp
public ChargeResponse Checkout(ChargeRequest request)
{
    IPaymentGateway gateway;

    switch (_settings.Provider)
    {
        case GatewayProvider.Asaas:
            gateway = new AsaasGateway(_settings.ApiKey, _settings.BaseUrl, _settings.Sandbox);
            break;
        case GatewayProvider.MercadoPago:
            gateway = new MercadoPagoGateway(_settings.ApiKey, _settings.BaseUrl);
            break;
        case GatewayProvider.Stripe:
            gateway = new StripeGateway(_settings.ApiKey);
            break;
    }

    return gateway.CreateCharge(request);
}
```

The `switch` is not the real problem. The `new` is.

- **The service cannot be unit tested.** It constructs the gateway itself, so there is no seam to substitute a fake.
  Testing checkout logic means having real credentials, or making real HTTP calls.
- **It knows things it should not.** That Asaas needs a sandbox flag and Stripe does not is a detail of those
  providers, and it has leaked into checkout.
- **Constructor changes ripple.** If `StripeGateway` needs an `HttpClient`, this file changes, and so does every other
  file that builds a gateway.
- **It gets copied.** The refund service and the reconciliation job need a gateway too, so the same block appears
  three times and drifts.

## Good example

[`GoodExample/`](GoodExample/)

```csharp
public interface IPaymentGatewayFactory
{
    IPaymentGateway Create(GatewayProvider provider);
    IPaymentGateway CreateDefault();
}
```

The service becomes uninteresting, which is the point:

```csharp
public sealed class CheckoutService
{
    private readonly IPaymentGatewayFactory _factory;

    public CheckoutService(IPaymentGatewayFactory factory) => _factory = factory;

    public ChargeResponse Checkout(ChargeRequest request) =>
        _factory.CreateDefault().CreateCharge(request);
}
```

The factory resolves from registrations rather than constructing anything:

```csharp
public PaymentGatewayFactory(IEnumerable<IPaymentGateway> gateways, GatewaySettings settings)
{
    _gateways = gateways.ToDictionary(gateway => gateway.Provider);
    _defaultProvider = settings.Provider;
}
```

This matters more than it looks. Because the container constructs the gateways, a gateway can take an `HttpClient`, an
`ILogger` and an `IOptions<T>` without the factory ever learning about them.

## What actually changed

| | Bad | Good |
| --- | --- | --- |
| Unit testing checkout | Needs real credentials | Pass a fake `IPaymentGateway` |
| Adding a provider | Edit checkout | Add a class, add a registration |
| Gateway needs a new dependency | Every call site changes | Only the registration changes |
| Unknown provider | `NotSupportedException` with no context | Message lists what is registered |

The test showing why this was worth doing is
[`TheServiceCanBeTestedWithAFakeGateway`](../../tests/DesignPatterns.Tests/Creational/FactoryTests.cs). It exercises
the full checkout path with no provider, no network and no credentials.

## The variants, and which one you actually want

"Factory" covers several distinct patterns that get conflated:

| Variant | What it is | When |
| --- | --- | --- |
| **Simple Factory** | A class with a `Create` method containing the decision. Not a GoF pattern. | Most of the time. This is what the good example uses. |
| **Factory Method** | A base class defers instantiation to subclasses overriding a `protected abstract Create()`. | When the creating class is itself part of an inheritance hierarchy. Rare in modern C#. |
| **Abstract Factory** | An interface creating a *family* of related objects that must be used together. | A `IUiThemeFactory` producing a matching button, panel and menu. Genuinely rare. |

Most codebases that say "we use Abstract Factory" are using Simple Factory. That is fine. Using the simpler thing is
not a failure.

## When to use it

- The concrete type is chosen at runtime from configuration or data
- Construction requires knowledge you do not want spread across the codebase
- You need a seam to substitute test doubles
- The same construction logic appears in more than one place

## When not to use it

- **The DI container already does it.** If the class needs one implementation and the container can inject it, ask for
  the interface directly. A factory that wraps a single `services.AddScoped<IThing, Thing>()` adds a layer and removes
  nothing.
- **Construction is a single `new` with no decision.** `new Order(customerId)` does not need a factory.
- **You are writing a factory to avoid `new` on principle.** `new` is not a code smell. `new` on a type you want to
  substitute in tests is.
- **The factory ends up with a `switch` on a type parameter and casting.** That is usually a sign the abstraction is
  wrong, not that the factory needs to be cleverer.

## Factory vs Strategy

They are structurally similar and often appear together, but answer different questions:

| | Factory | Strategy |
| --- | --- | --- |
| Question | Which object do I create? | Which algorithm do I run? |
| Returns | An object | A result |
| Lifetime | Caller keeps the object | Usually one call |

In this repository, [Strategy](../../behavioral/Strategy/) picks how to charge, and Factory picks who to charge
through. A real checkout uses both.

## The DI container is a factory

In .NET the container is already a factory, and often the only one you need:

```csharp
services.AddKeyedScoped<IPaymentGateway, AsaasGateway>(GatewayProvider.Asaas);
services.AddKeyedScoped<IPaymentGateway, StripeGateway>(GatewayProvider.Stripe);

// then
public CheckoutService([FromKeyedServices(GatewayProvider.Asaas)] IPaymentGateway gateway)
```

Keyed services, available since .NET 8, cover a good share of what people write factories for. Write the explicit
factory when you need the extra behaviour: a default, a useful error message, caching, or a decision the container
cannot express.
