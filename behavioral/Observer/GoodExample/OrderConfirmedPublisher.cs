namespace DesignPatterns.Behavioral.Observer.GoodExample;

public sealed record HandlerFailure(string HandlerName, Exception Exception);

/// <summary>
/// The subject. It knows the contract and nothing about the handlers.
///
/// Handlers are isolated from each other on purpose. A side effect is not
/// allowed to break an unrelated side effect: a bounced confirmation email
/// must not prevent the stock from being decreased.
///
/// Failures are collected and returned rather than swallowed, so the caller
/// decides whether a partial success is acceptable. Silently ignoring them
/// would be worse than the original coupling.
/// </summary>
public sealed class OrderConfirmedPublisher
{
    private readonly IReadOnlyCollection<IOrderConfirmedHandler> _handlers;

    public OrderConfirmedPublisher(IEnumerable<IOrderConfirmedHandler> handlers)
    {
        _handlers = handlers.ToList();
    }

    public IReadOnlyCollection<HandlerFailure> Publish(OrderConfirmed order)
    {
        var failures = new List<HandlerFailure>();

        foreach (var handler in _handlers)
        {
            try
            {
                handler.Handle(order);
            }
            catch (Exception exception)
            {
                failures.Add(new HandlerFailure(handler.Name, exception));
            }
        }

        return failures;
    }
}

/// <summary>
/// The business operation, back to one responsibility. Confirming an order
/// records the confirmation and announces it. What anyone does about that
/// announcement is not this class's concern.
/// </summary>
public sealed class OrderService
{
    private readonly OrderConfirmedPublisher _publisher;

    public OrderService(OrderConfirmedPublisher publisher)
    {
        _publisher = publisher;
    }

    public IReadOnlyCollection<HandlerFailure> Confirm(OrderConfirmed order) => _publisher.Publish(order);
}
