# Observer

## The problem it solves

One thing happens, and several unrelated things must react. The thing that happened should not have to know about
any of them.

## The scenario

Confirming an order must send an email, decrease stock, write an audit entry and push a notification. Tomorrow it
also has to tell the warehouse.

## Bad example

[`BadExample/OrderService.cs`](BadExample/OrderService.cs)

```csharp
public sealed class OrderService
{
    public OrderService(IEmailSender email, IStockLedger stock, IAuditLog audit, IPushNotifier push) { ... }

    public void Confirm(OrderConfirmed order)
    {
        _audit.Record(...);
        _email.Send(...);
        foreach (var line in order.Lines) _stock.Decrease(line.Sku, line.Quantity);
        _push.Notify(...);

        // And the next requirement lands right here.
    }
}
```

- **Four dependencies to test one operation.** Testing "confirm an order" means constructing four test doubles for
  behaviour that is not being tested.
- **One failure kills the rest.** The email provider is down, the exception propagates, and the stock is never
  decreased. The order is confirmed in the database and wrong in the warehouse.
- **The constructor grows forever.** Every new reaction is a new parameter and a new edit to a working class.
- **Ordering is implicit.** Nothing says why audit comes before email, and nothing stops someone reordering it.

## Good example

[`GoodExample/`](GoodExample/)

One handler per reaction, each with only the dependencies it needs:

```csharp
public sealed class DecreaseStockHandler : IOrderConfirmedHandler
{
    private readonly IStockLedger _stock;

    public string Name => "stock";

    public void Handle(OrderConfirmed order)
    {
        foreach (var line in order.Lines) _stock.Decrease(line.Sku, line.Quantity);
    }
}
```

The subject announces and steps back:

```csharp
public IReadOnlyCollection<HandlerFailure> Publish(OrderConfirmed order)
{
    var failures = new List<HandlerFailure>();

    foreach (var handler in _handlers)
    {
        try { handler.Handle(order); }
        catch (Exception exception) { failures.Add(new HandlerFailure(handler.Name, exception)); }
    }

    return failures;
}
```

## The two decisions that matter

Most Observer examples stop at the loop. These two lines are where the pattern earns its keep or quietly breaks your
system.

**Handlers are isolated from each other.** A bounced confirmation email must not stop the stock from being decreased.
Without the `try`, the pattern gives you nothing over the bad version: one handler still takes down the rest, just
with more files.

**Failures are returned, not swallowed.** A bare `catch { }` would be worse than the original coupling, because now
things fail silently. The publisher collects failures and hands them to the caller, who decides whether a partial
success is acceptable, and logs or retries accordingly.

The test [`AFailingHandlerDoesNotPreventTheOthers`](../../tests/DesignPatterns.Tests/Behavioral/ObserverTests.cs)
pins both.

## What actually changed

| | Bad | Good |
| --- | --- | --- |
| Dependencies of the service | Four, growing | One publisher |
| Email provider is down | Stock never updated | Stock updated, failure reported |
| Adding a reaction | Edit the service and its constructor | Add a class and a registration |
| Testing one reaction | Build four doubles | Build one |
| Testing the service | Needs every collaborator | Needs a publisher |

## When to use it

- One event, several independent reactions
- The set of reactions changes more often than the event does
- The reactions genuinely do not depend on each other
- You want to add behaviour without editing the thing that triggers it

## When not to use it

- **The reactions must all succeed or all fail.** If decreasing stock and charging the card must be atomic, they are
  not observers, they are steps in a transaction. Publishing them separately gives you partial failure, which is
  exactly what you were trying to avoid. Use a transaction, or the
  [Chain of Responsibility](../ChainOfResponsibility/) if the steps are sequential validations.
- **There is exactly one reaction and there always will be.** Calling the method is clearer.
- **Order matters between handlers.** Observer deliberately says nothing about ordering. If B must run after A, you
  have a pipeline, not a set of observers, and encoding that dependency through registration order is fragile.
- **You need the results back.** An observer returns nothing to the subject by design. Wanting a return value means
  this is a call, not a notification.

## The cost, stated honestly

Control flow becomes harder to follow. In the bad version you read `Confirm` and see everything that happens. In the
good version you read `Confirm`, see `Publish`, and then have to find the handlers.

That is a real trade. It is worth making when reactions change often or must be isolated from each other. It is not
worth making for two stable side effects that will never fail independently.

Keeping handler names in the interface (`Name => "stock"`) helps: failures and logs say which handler, not just which
exception.

## In .NET specifically

You rarely need to hand roll this:

| Mechanism | Use when |
| --- | --- |
| **C# `event`** | In process, few subscribers, no DI. Watch for memory leaks from subscribers that never unsubscribe. |
| **`IObservable<T>` / Rx** | Streams of events where you want composition: filtering, buffering, throttling. |
| **MediatR notifications** | You already use MediatR. `INotificationHandler<T>` is exactly this pattern, and the container does the wiring. |
| **A message broker** | Handlers must survive a process restart, run on another machine, or retry independently. This is the point at which in process observers stop being enough. |
| **Hand rolled, like here** | You want explicit control over isolation and failure reporting, without a dependency. |

The version in this folder is the hand rolled one because it makes the isolation decision visible. In production,
reach for MediatR or a broker before writing your own.

## Related

- [Mediator](../Mediator/) also decouples components, but routes a request to one handler and can return a result
- [Chain of Responsibility](../ChainOfResponsibility/) passes one request through ordered handlers that can stop it
