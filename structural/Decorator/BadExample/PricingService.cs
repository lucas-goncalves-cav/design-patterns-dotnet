namespace DesignPatterns.Structural.Decorator.BadExample;

/// <summary>
/// One class doing pricing, caching, logging and timing.
///
/// The pricing rule is four lines. The other thirty are infrastructure that
/// has nothing to do with pricing, and all of it has to be reasoned about
/// every time the pricing rule changes.
///
/// Worse, none of it is reusable: the next service that needs caching and
/// logging copies this code.
/// </summary>
public sealed class PricingService : IPricingService
{
    private readonly Dictionary<string, (Quote Quote, DateTime ExpiresAt)> _cache = [];
    private readonly IClock _clock;
    private readonly ILogSink _log;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public PricingService(IClock clock, ILogSink log)
    {
        _clock = clock;
        _log = log;
    }

    public Quote GetQuote(string sku)
    {
        _log.Write($"GetQuote started for {sku}.");
        var startedAt = _clock.UtcNow;

        try
        {
            if (_cache.TryGetValue(sku, out var cached) && cached.ExpiresAt > _clock.UtcNow)
            {
                _log.Write($"Cache hit for {sku}.");
                return cached.Quote;
            }

            _log.Write($"Cache miss for {sku}.");

            // The only part of this class that is about pricing.
            var price = CalculatePrice(sku);
            var quote = new Quote(sku, price, _clock.UtcNow);

            _cache[sku] = (quote, _clock.UtcNow.Add(_cacheDuration));

            return quote;
        }
        catch (Exception exception)
        {
            _log.Write($"GetQuote failed for {sku}: {exception.Message}");
            throw;
        }
        finally
        {
            var elapsed = _clock.UtcNow - startedAt;
            _log.Write($"GetQuote for {sku} took {elapsed.TotalMilliseconds:F0}ms.");
        }
    }

    private static decimal CalculatePrice(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU is required.", nameof(sku));
        }

        return 10m + sku.Length * 2.5m;
    }
}
