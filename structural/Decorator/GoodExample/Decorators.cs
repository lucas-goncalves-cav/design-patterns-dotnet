namespace DesignPatterns.Structural.Decorator.GoodExample;

/// <summary>
/// The real implementation, and nothing else. Pricing is four lines because
/// pricing is four lines.
/// </summary>
public sealed class PricingService : IPricingService
{
    private readonly IClock _clock;

    public PricingService(IClock clock)
    {
        _clock = clock;
    }

    public Quote GetQuote(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU is required.", nameof(sku));
        }

        return new Quote(sku, 10m + sku.Length * 2.5m, _clock.UtcNow);
    }
}

/// <summary>
/// A decorator implements the same interface it wraps. That is what lets it be
/// inserted anywhere the interface is expected, including in front of another
/// decorator.
/// </summary>
public sealed class LoggingPricingService : IPricingService
{
    private readonly IPricingService _inner;
    private readonly ILogSink _log;

    public LoggingPricingService(IPricingService inner, ILogSink log)
    {
        _inner = inner;
        _log = log;
    }

    public Quote GetQuote(string sku)
    {
        _log.Write($"GetQuote started for {sku}.");

        try
        {
            var quote = _inner.GetQuote(sku);

            _log.Write($"GetQuote succeeded for {sku}.");

            return quote;
        }
        catch (Exception exception)
        {
            _log.Write($"GetQuote failed for {sku}: {exception.Message}");
            throw;
        }
    }
}

public sealed class CachingPricingService : IPricingService
{
    private readonly IPricingService _inner;
    private readonly IClock _clock;
    private readonly TimeSpan _duration;
    private readonly Dictionary<string, (Quote Quote, DateTime ExpiresAt)> _cache = [];

    public CachingPricingService(IPricingService inner, IClock clock, TimeSpan? duration = null)
    {
        _inner = inner;
        _clock = clock;
        _duration = duration ?? TimeSpan.FromMinutes(5);
    }

    public Quote GetQuote(string sku)
    {
        if (_cache.TryGetValue(sku, out var cached) && cached.ExpiresAt > _clock.UtcNow)
        {
            return cached.Quote;
        }

        var quote = _inner.GetQuote(sku);

        _cache[sku] = (quote, _clock.UtcNow.Add(_duration));

        return quote;
    }
}

public sealed class TimingPricingService : IPricingService
{
    private readonly IPricingService _inner;
    private readonly IClock _clock;
    private readonly ILogSink _log;

    public TimingPricingService(IPricingService inner, IClock clock, ILogSink log)
    {
        _inner = inner;
        _clock = clock;
        _log = log;
    }

    public Quote GetQuote(string sku)
    {
        var startedAt = _clock.UtcNow;

        try
        {
            return _inner.GetQuote(sku);
        }
        finally
        {
            var elapsed = _clock.UtcNow - startedAt;
            _log.Write($"GetQuote for {sku} took {elapsed.TotalMilliseconds:F0}ms.");
        }
    }
}

/// <summary>
/// A retry decorator, to show that composition is not limited to the usual
/// logging and caching pair.
/// </summary>
public sealed class RetryingPricingService : IPricingService
{
    private readonly IPricingService _inner;
    private readonly int _maxAttempts;

    public RetryingPricingService(IPricingService inner, int maxAttempts = 3)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "At least one attempt is required.");
        }

        _inner = inner;
        _maxAttempts = maxAttempts;
    }

    public Quote GetQuote(string sku)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return _inner.GetQuote(sku);
            }
            catch (Exception) when (attempt < _maxAttempts)
            {
                // Retrying a transient failure. An ArgumentException would be
                // retried too here, which is why a real implementation filters
                // on exception type rather than catching everything.
            }
        }
    }
}
