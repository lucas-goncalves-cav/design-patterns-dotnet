namespace DesignPatterns.Structural.Decorator;

public sealed record Quote(string Sku, decimal Price, DateTime RetrievedAt);

/// <summary>
/// The service being decorated. A single method, so the decorators stay small
/// enough to read in one screen.
/// </summary>
public interface IPricingService
{
    Quote GetQuote(string sku);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public interface ILogSink
{
    void Write(string message);
}
