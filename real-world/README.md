# Real World

Patterns are usually shown one at a time. In production they arrive together, and the interesting question stops being
"what is Strategy" and becomes "which pattern owns which decision".

## The checkout

[`OrderCheckout/CheckoutPipeline.cs`](OrderCheckout/CheckoutPipeline.cs) implements one operation with four of the
patterns from this repository:

```
CheckoutRequest
      |
      v
[ Chain of Responsibility ]   validate: basket, customer, stock, coupon, credit, payment
      |                        stops at the first problem, cheap checks first
      | passed
      v
[ Strategy ]                  charge using the selected payment method
      |                        PIX, credit card, boleto or bank transfer
      | charged
      v
[ Observer ]                  announce the confirmed order
      |                        audit, email, stock, each isolated from the others
      v
CheckoutOutcome
```

[Factory](../creational/Factory/) sits one level up, choosing which payment gateway the strategies talk to.

## Each pattern owns exactly one decision

This is the part worth studying. No pattern is doing another one's job:

| Pattern | Owns | Knows nothing about |
| --- | --- | --- |
| Chain of Responsibility | Whether the order is allowed | Payment methods, side effects |
| Strategy | How much the payment costs | Stock, customers, coupons |
| Observer | What happens after confirmation | Validation, payment fees |
| Factory | Which gateway to use | Everything else |

When a pattern starts knowing about another's concern, that is the signal the boundary is wrong.

## The failure modes are deliberately different

Three kinds of failure, three different responses, and that is the whole point of combining them this way:

**Validation failure stops everything.** No charge, no side effects, an outcome saying which step rejected and why.

```csharp
outcome.Succeeded.Should().BeFalse();
outcome.RejectedStep.Should().Be("stock");
harness.Stock.Movements.Should().BeEmpty();
```

**Payment failure throws.** There is no partially charged order.

**Side effect failure does neither.** The order is paid and confirmed. A bounced welcome email must not undo that, so
the failure is collected and returned:

```csharp
var harness = Build(new ThrowingEmailSender());

var outcome = harness.Pipeline.Checkout(ValidRequest());

outcome.Succeeded.Should().BeTrue();          // the order stands
harness.Stock.Movements.Should().HaveCount(2); // other handlers still ran
outcome.SideEffectFailures.Single().HandlerName.Should().Be("email");
```

Getting this wrong in either direction is the common production bug: either an email outage rolls back paid orders, or
failures are swallowed and nobody notices the warehouse never heard about the order.

## Running it

```bash
dotnet test --filter "FullyQualifiedName~RealWorld"
```

The tests in [`CheckoutPipelineTests.cs`](../tests/DesignPatterns.Tests/RealWorld/CheckoutPipelineTests.cs) cover the
happy path, each rejection path, coupons, installments and the isolated side effect failure.

## What this example is not

It is not a complete checkout. There is no persistence, no idempotency key, no distributed transaction, no retry of
the payment call, no outbox for the side effects.

A real implementation would publish the confirmation through a message broker rather than in process, precisely so
that a failed handler can be retried independently. That is the honest next step past
[Observer](../behavioral/Observer/), and the point at which in process patterns stop being enough.
