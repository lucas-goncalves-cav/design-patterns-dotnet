namespace DesignPatterns.Behavioral.Observer.BadExample;

/// <summary>
/// Confirming an order does four unrelated things, and the method grows every
/// time the business thinks of a fifth.
///
/// What goes wrong:
///
///   - The service depends on email, stock, auditing and push notifications,
///     so testing "confirm an order" needs four test doubles
///   - A failure in the email provider prevents the stock from being updated,
///     because they share one try block and one call stack
///   - Adding "notify the warehouse" means editing this method again
///   - The order of the side effects is implicit and easy to get wrong
/// </summary>
public sealed class OrderService
{
    private readonly IEmailSender _email;
    private readonly IStockLedger _stock;
    private readonly IAuditLog _audit;
    private readonly IPushNotifier _push;

    public OrderService(IEmailSender email, IStockLedger stock, IAuditLog audit, IPushNotifier push)
    {
        _email = email;
        _stock = stock;
        _audit = audit;
        _push = push;
    }

    public void Confirm(OrderConfirmed order)
    {
        // The actual business operation is one line. Everything below is
        // consequence, not cause.
        _audit.Record($"Order {order.OrderId} confirmed for {order.Total:C}.");

        _email.Send(
            order.CustomerEmail,
            $"Order {order.OrderId} confirmed",
            $"Thank you. Your order totalling {order.Total:C} has been confirmed.");

        foreach (var line in order.Lines)
        {
            _stock.Decrease(line.Sku, line.Quantity);
        }

        _push.Notify(order.OrderId, "Your order has been confirmed.");

        // And the next requirement lands right here.
    }
}
