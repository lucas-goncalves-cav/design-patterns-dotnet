namespace DesignPatterns.Behavioral.Observer.GoodExample;

/// <summary>
/// An observer. One reaction to one event, with only the dependencies that
/// reaction actually needs.
/// </summary>
public interface IOrderConfirmedHandler
{
    string Name { get; }

    void Handle(OrderConfirmed order);
}

public sealed class AuditOrderConfirmedHandler : IOrderConfirmedHandler
{
    private readonly IAuditLog _audit;

    public AuditOrderConfirmedHandler(IAuditLog audit)
    {
        _audit = audit;
    }

    public string Name => "audit";

    public void Handle(OrderConfirmed order) =>
        _audit.Record($"Order {order.OrderId} confirmed for {order.Total:C}.");
}

public sealed class SendConfirmationEmailHandler : IOrderConfirmedHandler
{
    private readonly IEmailSender _email;

    public SendConfirmationEmailHandler(IEmailSender email)
    {
        _email = email;
    }

    public string Name => "email";

    public void Handle(OrderConfirmed order) =>
        _email.Send(
            order.CustomerEmail,
            $"Order {order.OrderId} confirmed",
            $"Thank you. Your order totalling {order.Total:C} has been confirmed.");
}

public sealed class DecreaseStockHandler : IOrderConfirmedHandler
{
    private readonly IStockLedger _stock;

    public DecreaseStockHandler(IStockLedger stock)
    {
        _stock = stock;
    }

    public string Name => "stock";

    public void Handle(OrderConfirmed order)
    {
        foreach (var line in order.Lines)
        {
            _stock.Decrease(line.Sku, line.Quantity);
        }
    }
}

public sealed class PushNotificationHandler : IOrderConfirmedHandler
{
    private readonly IPushNotifier _push;

    public PushNotificationHandler(IPushNotifier push)
    {
        _push = push;
    }

    public string Name => "push";

    public void Handle(OrderConfirmed order) =>
        _push.Notify(order.OrderId, "Your order has been confirmed.");
}
