# Chain of Responsibility

## The problem it solves

A request must pass through a sequence of steps, any of which can stop it. The steps change, and their order matters.

## The scenario

Before an order is created it has to be validated: the basket is not empty, the customer exists and is not blocked,
there is stock, the coupon is valid, the total fits the credit limit, and the payment authorizes.

## Bad example

[`BadExample/OrderValidator.cs`](BadExample/OrderValidator.cs)

```csharp
public OrderValidationResult Validate(OrderRequest request)
{
    if (request.Items.Count == 0) return Failure("basket", ...);
    if (request.Items.Any(item => item.Quantity <= 0)) return Failure("basket", ...);
    if (!_customers.Exists(request.CustomerId)) return Failure("customer", ...);
    if (_customers.IsBlocked(request.CustomerId)) return Failure("customer", ...);

    foreach (var item in request.Items) { ... }

    if (request.CouponCode is not null)
    {
        if (!_coupons.IsValid(request.CouponCode)) return Failure("coupon", ...);
        total *= 0.9m;
    }

    if (total > _customers.CreditLimit(...)) return Failure("credit", ...);
    if (!_payment.Authorize(...)) return Failure("payment", ...);

    return Success();
}
```

Using early returns instead of nesting already makes this the good version of the bad version. It is still:

- **Four dependencies to test one rule.** Testing "a blocked customer is rejected" requires stock, payment and coupon
  doubles that have nothing to do with the assertion.
- **Order encoded in position.** Moving the payment check earlier means moving code, and the coupon block mutates
  `total`, so moving things is riskier than it looks.
- **Not composable.** A B2B flow needing five of the six checks cannot reuse this. It gets a flag, then two flags.
- **Growing.** Each new rule is another edit to a method every order already depends on.

## Good example

[`GoodExample/`](GoodExample/)

One handler per rule, with only the dependency that rule needs:

```csharp
public sealed class CustomerHandler : OrderValidationHandler
{
    private readonly ICustomerRepository _customers;

    public override string Step => "customer";

    protected override OrderValidationResult Check(OrderValidationContext context)
    {
        if (!_customers.Exists(context.Request.CustomerId)) return Reject($"Customer ... was not found.");
        if (_customers.IsBlocked(context.Request.CustomerId)) return Reject("Customer is blocked.");

        return Accept();
    }
}
```

The base class owns the "stop or continue" decision, so no handler has to remember it:

```csharp
public OrderValidationResult Handle(OrderValidationContext context)
{
    var result = Check(context);

    if (!result.Succeeded) return result;

    return _next?.Handle(context) ?? OrderValidationResult.Success();
}
```

And the pipeline becomes a list you can read:

```csharp
new OrderValidator(
[
    new BasketHandler(),
    new CustomerHandler(customers),
    new StockHandler(stock),
    new CouponHandler(coupons),
    new CreditLimitHandler(customers),
    new PaymentHandler(payment)
]);
```

## The part most examples leave out

Textbook Chain of Responsibility passes the request along unchanged. Real pipelines are not like that: one step
computes something the next steps need.

Here the coupon handler changes the total, and the credit and payment handlers must see the discounted value. That is
what `OrderValidationContext` is for:

```csharp
public sealed class OrderValidationContext
{
    public OrderRequest Request { get; }
    public decimal Total { get; set; }
}
```

The consequence is real and worth knowing before you copy this: **handlers are no longer fully independent.** Moving
`CouponHandler` after `CreditLimitHandler` silently changes the outcome. The tests
[`AValidCouponLowersTheTotalSeenByLaterSteps`](../../tests/DesignPatterns.Tests/Behavioral/ChainOfResponsibilityTests.cs)
and `PaymentIsAuthorizedForTheDiscountedTotal` exist to make that coupling explicit rather than accidental.

If your steps genuinely share no state, pass the request alone and keep them independent. Add a context only when a
step actually needs a value from an earlier one.

## Order the chain by cost

Cheap checks first, so an empty basket never reaches the payment provider:

```
basket -> customer -> stock -> coupon -> credit -> payment
   in memory            database          network
```

`ShortCircuitingStopsBeforeTheExpensiveSteps` asserts that the stock checker and the payment authorizer are called
zero times when the basket check fails.

## What actually changed

| | Bad | Good |
| --- | --- | --- |
| Testing one rule | Four test doubles | One |
| Reordering | Move code, risk the shared `total` | Reorder a list |
| A pipeline with fewer steps | Flags | A different list |
| Adding a rule | Edit a method everything depends on | Add a class |
| Which step rejected | A string literal per branch | `Step` on the handler |

## When to use it

- A sequence of checks or transformations where any step can stop the request
- The set or order of steps varies by context: B2C and B2B, sandbox and production
- Steps have unrelated dependencies and you want to test them separately
- You want cheap checks to short circuit before expensive ones

## When not to use it

- **Every step must run.** Chain of Responsibility is built around stopping early. If you need all failures, not just
  the first, collect results instead. That is a validator, and FluentValidation already does it well.
- **Two or three stable steps.** Three `if` statements are clearer than three classes, a base class and a builder.
- **The steps are independent reactions, not a gate.** That is [Observer](../Observer/): several things react to one
  event and none of them stops the others.
- **You need to know which handler will run before running it.** The chain decides dynamically. If the decision is
  known up front, [Strategy](../Strategy/) is more direct.

## The cost, stated honestly

The flow is no longer readable in one place. In the bad version, `Validate` shows every rule in order. In the good
version you read a list of class names and then go find them.

Two things reduce that cost:

- Keeping the pipeline definition in one obvious factory (`CreateDefault`) so the order is still readable in a single
  screen
- Putting the step name on the handler, so a rejection says which step and a stack trace names a class instead of a
  line number

## The pipeline variant

The classic form links handlers through a `_next` reference, which is what this example uses because it is the
pattern. In modern C# the same thing is often written as a list plus a fold, or as middleware:

```csharp
public OrderValidationResult Validate(OrderRequest request)
{
    var context = new OrderValidationContext(request);

    foreach (var handler in _handlers)
    {
        var result = handler.Check(context);

        if (!result.Succeeded) return result;
    }

    return OrderValidationResult.Success();
}
```

Simpler, and enough when handlers only run before the next one. The linked form earns its extra indirection when a
handler needs to do something **after** the rest of the chain: wrap it in a transaction, time it, catch its
exceptions. That is exactly what ASP.NET Core middleware is, and why it is written that way:

```csharp
app.Use(async (context, next) =>
{
    // before
    await next();
    // after
});
```

## In .NET specifically

| Mechanism | Use when |
| --- | --- |
| **ASP.NET Core middleware** | The chain is HTTP request processing |
| **`DelegatingHandler`** | The chain is outbound HTTP: auth headers, retries, logging |
| **MediatR `IPipelineBehavior<,>`** | You already use MediatR and want validation, logging or transactions around handlers |
| **FluentValidation** | You need every failure, not just the first |
| **Hand rolled, like here** | Domain specific steps with per step dependencies |

## Related

- [Decorator](../../structural/Decorator/) also nests objects, but every wrapper delegates inward. A chain handler can
  stop the request entirely.
- [Observer](../Observer/) notifies independent handlers that cannot stop each other
- [Strategy](../Strategy/) picks one implementation up front instead of trying several in order
