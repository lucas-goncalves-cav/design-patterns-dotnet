# Decorator

## The problem it solves

You want to add behaviour to an object without changing it, and without a subclass for every combination.

## The scenario

A pricing service needs logging, caching, timing and retries. None of those are pricing.

## Bad example

[`BadExample/PricingService.cs`](BadExample/PricingService.cs)

```csharp
public Quote GetQuote(string sku)
{
    _log.Write($"GetQuote started for {sku}.");
    var startedAt = _clock.UtcNow;

    try
    {
        if (_cache.TryGetValue(sku, out var cached) && cached.ExpiresAt > _clock.UtcNow) { ... }

        var price = CalculatePrice(sku);   // the only pricing line in the class
        _cache[sku] = (quote, _clock.UtcNow.Add(_cacheDuration));
        return quote;
    }
    catch (Exception exception) { _log.Write(...); throw; }
    finally { _log.Write($"... took {elapsed.TotalMilliseconds:F0}ms."); }
}
```

The pricing rule is one line. The other thirty are infrastructure, and all of it has to be read and reasoned about
every time the pricing rule changes.

- **Four responsibilities in one class.** Pricing, caching, logging and timing.
- **Nothing is reusable.** The next service that needs caching copies this code, and the two copies drift.
- **Everything is coupled.** Turning off caching in one environment means a flag, then a second flag, then a
  configuration class.
- **Testing pricing means testing all of it.** There is no way to exercise the pricing rule without the cache.

## Good example

[`GoodExample/Decorators.cs`](GoodExample/Decorators.cs)

The service does its job and nothing else:

```csharp
public sealed class PricingService : IPricingService
{
    public Quote GetQuote(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU is required.", nameof(sku));

        return new Quote(sku, 10m + sku.Length * 2.5m, _clock.UtcNow);
    }
}
```

Each decorator implements the same interface it wraps, which is what lets it sit anywhere the interface is expected:

```csharp
public sealed class CachingPricingService : IPricingService
{
    private readonly IPricingService _inner;

    public Quote GetQuote(string sku)
    {
        if (_cache.TryGetValue(sku, out var cached) && cached.ExpiresAt > _clock.UtcNow) return cached.Quote;

        var quote = _inner.GetQuote(sku);
        _cache[sku] = (quote, _clock.UtcNow.Add(_duration));
        return quote;
    }
}
```

Behaviour is assembled at the wiring site:

```csharp
IPricingService service =
    new LoggingPricingService(
        new CachingPricingService(
            new TimingPricingService(
                new PricingService(clock),
                clock, log),
            clock),
        log);
```

## Order is a decision, not an accident

This is the part most explanations skip, and it is the part that bites.

```
Cache outside timing  ->  a cache hit never reaches the timer
                          you measure only real work

Timing outside cache  ->  every call is measured, hits included
                          you measure what the caller experiences
```

Both are correct. They answer different questions, and picking the wrong one gives you a dashboard that quietly lies.
The test [`OrderOfCompositionChangesWhatIsMeasured`](../../tests/DesignPatterns.Tests/Structural/DecoratorTests.cs)
pins both behaviours so the choice stays deliberate.

A useful default, outermost first:

```
Logging  ->  Retry  ->  Cache  ->  Timing  ->  Real implementation
```

Logging sees everything. Retry wraps the cache so a retry can still be served from cache. Timing measures actual work.

## What actually changed

| | Bad | Good |
| --- | --- | --- |
| Responsibilities per class | Four | One |
| Reusing caching elsewhere | Copy and paste | Wrap a different service |
| Disabling caching in dev | A flag inside the class | Do not add the decorator |
| Testing the pricing rule | Through the cache | Directly |
| Combinations of behaviour | Flags multiplying | Composition at wiring time |

## When to use it

- Cross cutting concerns: logging, caching, retries, metrics, authorization, validation
- You want behaviour to be optional per environment or per registration
- Subclassing would produce a combinatorial explosion (`CachedLoggedRetryingPricingService`)
- The interface is small. Decorators get tedious past five or six methods.

## When not to use it

- **The interface is wide.** Decorating a fifteen method interface means fifteen pass through methods per decorator.
  Split the interface first, or use an interceptor.
- **Only one combination will ever exist.** If logging is always on and caching is always on, the composition buys
  flexibility nobody will use, at the cost of four files and an indirection.
- **The decorator needs to change the contract.** Adding a parameter or a return value means it is not a decorator.
  That is an [Adapter](../Adapter/) or a new abstraction.
- **The framework already does it.** See below.

## The cost, stated honestly

Stack traces get deeper and less readable. Debugging steps through three wrappers before reaching the code you care
about. Finding "what actually runs" means reading the DI registration, not the class.

That is a real trade. It is worth it when the concerns genuinely vary independently. It is not worth it to avoid four
lines in one method.

## In .NET specifically

You often do not need to write these by hand:

| Concern | Prefer |
| --- | --- |
| Logging, metrics, tracing | `ILogger`, `ActivitySource`, OpenTelemetry auto instrumentation |
| Retry, circuit breaker, timeout | **Polly**, via `IHttpClientFactory` resilience handlers |
| Caching | `IMemoryCache` or `HybridCache`, often behind a decorator like this one |
| Cross cutting on many types | **Scrutor** for `services.Decorate<IService, LoggingService>()`, or a DI interceptor |

`Scrutor` is worth knowing. It turns the nested constructor calls into a readable chain:

```csharp
services.AddScoped<IPricingService, PricingService>();
services.Decorate<IPricingService, TimingPricingService>();
services.Decorate<IPricingService, CachingPricingService>();
services.Decorate<IPricingService, LoggingPricingService>();
```

Registration order is inside out: the last `Decorate` call is the outermost wrapper.

`HttpClient` in .NET is itself a decorator chain. `DelegatingHandler` is exactly this pattern, and adding a handler
to a named client is adding a decorator.

## Decorator vs Proxy vs Adapter

All three wrap an object and implement an interface. The intent differs:

| Pattern | Interface | Intent |
| --- | --- | --- |
| **Decorator** | Same as wrapped | Add behaviour |
| **Proxy** | Same as wrapped | Control access: lazy loading, remoting, permissions |
| **[Adapter](../Adapter/)** | Different | Make an incompatible interface fit |

Decorator and Proxy are structurally identical. The difference is why: a decorator adds something for the caller, a
proxy stands in for something the caller cannot reach directly.
