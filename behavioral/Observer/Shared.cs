namespace DesignPatterns.Behavioral.Observer;

public sealed record OrderConfirmed(
    Guid OrderId,
    string CustomerEmail,
    decimal Total,
    IReadOnlyCollection<OrderLine> Lines,
    DateTime ConfirmedAt);

public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);

/// <summary>
/// Collaborators the side effects need. Kept as interfaces so both examples
/// can be tested without touching a network or a database.
/// </summary>
public interface IEmailSender
{
    void Send(string to, string subject, string body);
}

public interface IStockLedger
{
    void Decrease(string sku, int quantity);
}

public interface IAuditLog
{
    void Record(string message);
}

public interface IPushNotifier
{
    void Notify(Guid orderId, string message);
}
