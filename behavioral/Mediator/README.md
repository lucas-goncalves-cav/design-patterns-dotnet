# Mediator

## The problem it solves

Components that need to talk to each other end up referencing each other directly. As the number grows, so does the
number of connections, and every component knows too much about the rest of the system.

## The scenario

A controller that registers customers. It needs to store, email, audit, record metrics, validate and log.

## Bad example

[`BadExample/CustomerController.cs`](BadExample/CustomerController.cs)

```csharp
public CustomerController(
    ICustomerStore store,
    IEmailService email,
    IAuditService audit,
    IMetricsRecorder metrics,
    IValidator validator,
    IAppLogger logger)
```

Six constructor parameters, and the next feature makes it seven. The method body mixes three different concerns:

```csharp
public CustomerRegistered Register(RegisterCustomerCommand command)
{
    _logger.Log($"Register started for {command.Email}.");     // cross cutting
    _metrics.Increment("customer.register.attempt");           // cross cutting

    var errors = _validator.Validate(command);                 // validation
    if (errors.Count > 0) throw new ArgumentException(...);

    if (_store.EmailTaken(command.Email)) throw ...;           // business rule
    var customer = new CustomerDetails(...);                   // orchestration
    _store.Save(customer);
    _email.SendWelcome(...);

    // plus the catch block repeating the logging and metrics
}
```

- **Six test doubles to test one endpoint.**
- **The logging and metrics wrapping is copied** into `Get`, and into every other controller, slightly differently.
- **Adding a concern edits every endpoint.** Adding transactions means touching all of them.
- **The controller does HTTP, validation, orchestration and observability.**

## Good example

[`GoodExample/`](GoodExample/)

The controller translates HTTP into a request and stops:

```csharp
public sealed class CustomerController
{
    private readonly IMediator _mediator;

    public CustomerRegistered Register(RegisterCustomerCommand command) =>
        _mediator.Send(new RegisterCustomer(command.Name, command.Email));

    public CustomerDetails? Get(GetCustomerQuery query) =>
        _mediator.Send(new GetCustomer(query.CustomerId));
}
```

One handler per use case, with only the dependencies that use case needs:

```csharp
public sealed class RegisterCustomerHandler : IRequestHandler<RegisterCustomer, CustomerRegistered>
{
    public RegisterCustomerHandler(ICustomerStore store, IWelcomeEmailSender email) { ... }

    public CustomerRegistered Handle(RegisterCustomer request)
    {
        if (_store.EmailTaken(request.Email)) throw new InvalidOperationException(...);

        var customer = new CustomerDetails(Guid.NewGuid(), request.Name, request.Email, Active: true);

        _store.Save(customer);
        _email.SendWelcome(customer.Email, customer.Name);

        return new CustomerRegistered(customer.CustomerId, customer.Name, customer.Email);
    }
}
```

## The pipeline is the real payoff

The mediator itself is not that interesting. What makes it worth the indirection is that cross cutting concerns become
a behaviour written once, wrapped around every handler:

```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public TResponse Handle(TRequest request, Func<TResponse> next)
    {
        _log.Write($"{typeof(TRequest).Name} started.");

        try
        {
            var response = next();
            _log.Write($"{typeof(TRequest).Name} succeeded.");
            return response;
        }
        catch (Exception exception)
        {
            _log.Write($"{typeof(TRequest).Name} failed: {exception.Message}");
            throw;
        }
    }
}
```

Behaviours nest outside in, and the handler runs last:

```
outer:before -> inner:before -> handler -> inner:after -> outer:after
```

That ordering is asserted in
[`BehavioursRunOutsideInAndTheHandlerRunsLast`](../../tests/DesignPatterns.Tests/Behavioral/MediatorTests.cs).

Validation, logging, metrics, transactions, caching and authorization all become behaviours. Adding one is adding a
class, not editing thirty endpoints.

## A detail worth copying

The mediator here resolves handlers by reflection, and reflection wraps everything a handler throws in a
`TargetInvocationException`. Left alone, that means a caller expecting `InvalidOperationException` gets something else
entirely, with the real error two levels down.

```csharp
catch (TargetInvocationException exception) when (exception.InnerException is not null)
{
    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
    throw; // Unreachable, the line above always throws.
}
```

`ExceptionDispatchInfo` rethrows the original exception with its stack trace intact, which a plain
`throw exception.InnerException` would destroy. Any reflection based dispatcher needs this, and it is the kind of
detail that only shows up once the tests are written.

## What actually changed

| | Bad | Good |
| --- | --- | --- |
| Controller dependencies | Six, growing | One |
| Testing one endpoint | Six test doubles | Two |
| Adding logging to everything | Edit every endpoint | Register one behaviour |
| Locating the code for a feature | Somewhere in a large controller | One named handler class |
| Validation | Inline, copied | A behaviour, composed |

## The honest cost

**Discoverability drops.** In the bad version, "go to definition" on `_store.Save` lands on the code. In the good
version you follow `_mediator.Send(new RegisterCustomer(...))` to nothing, then search the solution for
`RegisterCustomerHandler`. IDEs have improved at this, but it is still a real loss.

**It looks like indirection for its own sake on small projects.** Two endpoints and one concern do not need a mediator,
and adding one there is cargo cult.

**Compile time safety is weaker.** Forgetting to register a handler is a runtime failure, not a build error. Hence
the explicit error message: `No handler registered for GetCustomer.`

## When to use it

- Many use cases, each with different dependencies
- Cross cutting concerns you want applied uniformly: logging, validation, transactions, authorization
- Controllers are accumulating constructor parameters
- You want each use case in its own testable class
- You are moving towards CQRS, where commands and queries are already separate types

## When not to use it

- **A small application.** Two controllers and four services do not need it. Inject the services.
- **To decouple things that are not actually coupled.** If A calls B once, `A -> B` is clearer than
  `A -> mediator -> B`.
- **As a replacement for a service layer, with no behaviours.** If you are not using the pipeline, you have added
  indirection and received nothing. That is the most common way this pattern is misapplied.
- **Handlers calling other handlers.** Once `Handle` sends another request, which sends another, you have rebuilt the
  coupling you removed, now invisible to the compiler. Call a shared service instead.

## In .NET specifically

**Use [MediatR](https://github.com/jbogard/MediatR)**, do not write your own. The implementation here is deliberately
small and written out so the mechanism is visible; a real one needs async, cancellation tokens, streaming, notification
publishing and proper DI integration.

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

Note that MediatR changed to a commercial licence for new major versions in 2025. Existing versions remain available,
and alternatives include Wolverine and simple hand rolled dispatch. Check the current licensing before adopting it.

## Mediator vs Observer

Both reduce direct coupling between components. They are not interchangeable:

| | Mediator | [Observer](../Observer/) |
| --- | --- | --- |
| Handlers per message | One | Many |
| Returns a result | Yes | No |
| Sender expects something to happen | Yes | No |
| Typical name | Command, Query | Event, Notification |

`Send` goes to exactly one handler and returns its result. `Publish` goes to every subscriber and returns nothing.
MediatR has both, and mixing them up is a common source of confusion: a command that nobody handles is a bug, an event
that nobody handles is fine.

## Related

- [Observer](../Observer/) for one to many notification with no return value
- [Chain of Responsibility](../ChainOfResponsibility/) for a request passing through ordered steps that can stop it
- [Decorator](../../structural/Decorator/) is what a pipeline behaviour is, applied generically
