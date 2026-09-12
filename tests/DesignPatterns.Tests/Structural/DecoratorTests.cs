using DesignPatterns.Structural.Decorator;
using DesignPatterns.Structural.Decorator.GoodExample;
using FluentAssertions;
using BadPricing = DesignPatterns.Structural.Decorator.BadExample.PricingService;
using GoodPricing = DesignPatterns.Structural.Decorator.GoodExample.PricingService;

namespace DesignPatterns.Tests.Structural;

public class DecoratorTests
{
    [Fact]
    public void BothVersionsReturnTheSamePrice()
    {
        var clock = new FakeClock();

        var bad = new BadPricing(clock, new FakeLog()).GetQuote("SKU-123");
        var good = BuildFullStack(clock, new FakeLog()).GetQuote("SKU-123");

        good.Price.Should().Be(bad.Price);
        good.Sku.Should().Be(bad.Sku);
    }

    [Fact]
    public void TheUndecoratedServiceOnlyPrices()
    {
        var quote = new GoodPricing(new FakeClock()).GetQuote("ABC");

        quote.Sku.Should().Be("ABC");
        quote.Price.Should().Be(17.5m);
    }

    [Fact]
    public void CachingAvoidsASecondCallToTheInnerService()
    {
        var counting = new CountingPricingService();
        var service = new CachingPricingService(counting, new FakeClock());

        service.GetQuote("SKU-1");
        service.GetQuote("SKU-1");
        service.GetQuote("SKU-1");

        counting.Calls.Should().Be(1);
    }

    [Fact]
    public void CacheEntriesExpire()
    {
        var clock = new FakeClock();
        var counting = new CountingPricingService();
        var service = new CachingPricingService(counting, clock, TimeSpan.FromMinutes(5));

        service.GetQuote("SKU-1");
        clock.Advance(TimeSpan.FromMinutes(6));
        service.GetQuote("SKU-1");

        counting.Calls.Should().Be(2);
    }

    [Fact]
    public void LoggingRecordsSuccessAndFailure()
    {
        var log = new FakeLog();
        var service = new LoggingPricingService(new GoodPricing(new FakeClock()), log);

        service.GetQuote("SKU-1");
        var act = () => service.GetQuote("   ");

        act.Should().Throw<ArgumentException>();
        log.Messages.Should().Contain(message => message.Contains("succeeded"));
        log.Messages.Should().Contain(message => message.Contains("failed"));
    }

    [Fact]
    public void RetryingGivesUpAfterTheConfiguredAttempts()
    {
        var flaky = new FlakyPricingService(failuresBeforeSuccess: 5);
        var service = new RetryingPricingService(flaky, maxAttempts: 3);

        var act = () => service.GetQuote("SKU-1");

        act.Should().Throw<InvalidOperationException>();
        flaky.Calls.Should().Be(3);
    }

    [Fact]
    public void RetryingRecoversFromATransientFailure()
    {
        var flaky = new FlakyPricingService(failuresBeforeSuccess: 2);
        var service = new RetryingPricingService(flaky, maxAttempts: 3);

        var quote = service.GetQuote("SKU-1");

        quote.Sku.Should().Be("SKU-1");
        flaky.Calls.Should().Be(3);
    }

    /// <summary>
    /// The reason the pattern exists: behaviour is composed at the wiring site,
    /// and the order of composition is a decision you get to make.
    /// </summary>
    [Fact]
    public void OrderOfCompositionChangesWhatIsMeasured()
    {
        var clock = new FakeClock();
        var counting = new CountingPricingService();

        // Cache outside timing: a cache hit never reaches the timer.
        var log = new FakeLog();
        var cacheOutside = new CachingPricingService(
            new TimingPricingService(counting, clock, log),
            clock);

        cacheOutside.GetQuote("SKU-1");
        cacheOutside.GetQuote("SKU-1");

        log.Messages.Count(message => message.Contains("took")).Should().Be(1);

        // Timing outside cache: every call is measured, including cache hits.
        var otherLog = new FakeLog();
        var timingOutside = new TimingPricingService(
            new CachingPricingService(new CountingPricingService(), clock),
            clock,
            otherLog);

        timingOutside.GetQuote("SKU-2");
        timingOutside.GetQuote("SKU-2");

        otherLog.Messages.Count(message => message.Contains("took")).Should().Be(2);
    }

    [Fact]
    public void DecoratorsCanBeStackedInAnyDepth()
    {
        var clock = new FakeClock();
        var log = new FakeLog();

        var service = BuildFullStack(clock, log);

        var quote = service.GetQuote("SKU-999");

        quote.Price.Should().Be(27.5m);
        log.Messages.Should().Contain(message => message.Contains("started"));
        log.Messages.Should().Contain(message => message.Contains("took"));
    }

    private static IPricingService BuildFullStack(IClock clock, ILogSink log) =>
        new LoggingPricingService(
            new CachingPricingService(
                new TimingPricingService(
                    new GoodPricing(clock),
                    clock,
                    log),
                clock),
            log);

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed class FakeClock : IClock
    {
        private DateTime _now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class FakeLog : ILogSink
    {
        public List<string> Messages { get; } = [];

        public void Write(string message) => Messages.Add(message);
    }

    private sealed class CountingPricingService : IPricingService
    {
        public int Calls { get; private set; }

        public Quote GetQuote(string sku)
        {
            Calls++;

            return new Quote(sku, 42m, DateTime.UnixEpoch);
        }
    }

    private sealed class FlakyPricingService : IPricingService
    {
        private readonly int _failuresBeforeSuccess;

        public FlakyPricingService(int failuresBeforeSuccess) =>
            _failuresBeforeSuccess = failuresBeforeSuccess;

        public int Calls { get; private set; }

        public Quote GetQuote(string sku)
        {
            Calls++;

            if (Calls <= _failuresBeforeSuccess)
            {
                throw new InvalidOperationException("Upstream unavailable.");
            }

            return new Quote(sku, 42m, DateTime.UnixEpoch);
        }
    }
}
