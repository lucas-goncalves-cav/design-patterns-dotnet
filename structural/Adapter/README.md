# Adapter

## The problem it solves

You need to use a class whose interface you cannot change, and it does not fit the interface your code expects.

Almost always this is a third party SDK. You do not own it, you cannot change it, and its vocabulary is not yours.

## The scenario

The same application charges customers through Asaas, Stripe and Mercado Pago. The three SDKs disagree about
everything that matters:

| | Asaas | Stripe | Mercado Pago |
| --- | --- | --- | --- |
| Amount type | `decimal`, reais | `long`, **cents** | `double`, reais |
| Id type | `string` | `string` | `long` |
| Paid status | `"RECEIVED"`, `"CONFIRMED"` | `"succeeded"` | `"approved"` |
| Method field | `BillingType: "PIX"` | implicit | `PaymentMethodId: "pix"` |

Stand ins for the three SDKs are in [`GoodExample/ExternalSdks/`](GoodExample/ExternalSdks/), deliberately awkward in
the way real ones are.

## Bad example

[`BadExample/OrderService.cs`](BadExample/OrderService.cs)

```csharp
case "stripe":
    // Multiplying by 100 here, and hoping nobody forgets it elsewhere.
    var stripe = _stripe.Create(new StripePaymentIntentOptions
    {
        Amount = (long)(amount * 100),
        Currency = "brl"
    });
    return stripe.Id;
```

And the question that gets asked everywhere:

```csharp
public bool IsPaid(string provider, string externalId)
{
    switch (provider)
    {
        case "asaas":       return _asaas.GetPayment(externalId).Status is "RECEIVED" or "CONFIRMED";
        case "stripe":      return _stripe.Retrieve(externalId).Status == "succeeded";
        case "mercadopago": return _mercadoPago.Get(long.Parse(externalId)).StatusPagamento == "approved";
    }
}
```

Nothing here is a business rule, and all of it is now business logic:

- **The cents conversion is inline.** It appears wherever Stripe is called, and eventually one of those places is
  missing it. That bug charges a customer 100 times too much.
- **Provider spellings of "paid" leak everywhere.** Every place that asks "is this paid" repeats all three.
- **`long.Parse` on an id** throws `FormatException` at a call site that has no idea what a Mercado Pago id is.
- **Swapping providers touches business code**, so a commercial decision becomes a refactor.
- **Testing needs the SDKs.** They are constructed inline, so there is no seam.

## Good example

[`GoodExample/`](GoodExample/)

One interface, in the application's own vocabulary:

```csharp
public interface IPaymentGateway
{
    string Name { get; }
    Payment Create(CreatePaymentCommand command);
    Payment GetById(string externalId);
}
```

One adapter per SDK, and the translation stops there:

```csharp
private static long ToCents(decimal amount) => (long)decimal.Round(amount * CentsPerUnit, 0);

private static PaymentStatus ToPaymentStatus(string status) => status switch
{
    "requires_payment_method" or "processing" => PaymentStatus.Pending,
    "succeeded" => PaymentStatus.Paid,
    "canceled"  => PaymentStatus.Cancelled,
    _ => throw new NotSupportedException($"Unmapped Stripe status: {status}.")
};
```

Business logic becomes boring, which is the goal:

```csharp
public bool IsPaid(string externalId) => _gateway.GetById(externalId).Status == PaymentStatus.Paid;
```

## The detail worth copying

Look at the `_ =>` arm above. It **throws** instead of defaulting to `Pending`.

That is deliberate. If Stripe adds a status tomorrow, you want a loud failure in the adapter, not a paid order
silently treated as unpaid. A default arm in a status mapping is a bug that files itself under "works on my machine"
for six months.

The same applies to `long.TryParse` in the Mercado Pago adapter: the error names the problem
(`Mercado Pago ids are numeric, received 'pay_abc'`) instead of surfacing a bare `FormatException` three layers up.

## What actually changed

| | Bad | Good |
| --- | --- | --- |
| Cents conversion | Inline, repeated, forgettable | One private method, one place |
| "Is it paid" | Three spellings at every call site | One enum comparison |
| Adding a provider | Edit business logic | Add an adapter class |
| Swapping providers | Refactor | Change a registration |
| Unknown status | Silently wrong | Throws with the raw value |
| Testing | Needs real SDKs | Any `IPaymentGateway` |

## When to use it

- Integrating a third party SDK, API client or legacy component
- Two libraries do the same job with different interfaces and you want to swap them
- An external contract is unstable and you want the churn contained in one file
- You need a seam for testing something you cannot control

## When not to use it

- **You control both sides.** If the interface does not fit and you own it, fix the interface. An adapter between two
  classes you wrote is usually a design problem being papered over.
- **The adapter would be a pass through.** If every method is `_inner.Method(args)` with no translation, you have
  added a file and an indirection for nothing.
- **You are wrapping just in case.** Wrapping a stable, well designed dependency you have no intention of replacing,
  on the theory that you might one day, costs real complexity now for hypothetical flexibility later. `System.Text.Json`
  does not need an adapter.
- **One provider, forever.** With a single payment gateway and no plan for a second, the adapter still pays for itself
  through testability, but that is the argument to make, not "we might add Stripe".

## Adapter vs Facade vs Decorator

All three wrap something. The intent differs:

| Pattern | Interface after wrapping | Purpose |
| --- | --- | --- |
| **Adapter** | Different, the one you want | Make an incompatible thing fit |
| **Facade** | New and simpler | Hide a complicated subsystem behind one entry point |
| **[Decorator](../Decorator/)** | Same as the wrapped type | Add behaviour without changing the contract |

An adapter that also simplifies is doing both, and that is fine. The label matters less than being clear about what
the class is for.

## The name in real projects

You will also see this called an **anti corruption layer**, the term from Domain Driven Design. Same idea at a larger
scale: a translation boundary that stops another system's model from leaking into yours.

## Related

- [Factory](../../creational/Factory/) chooses which adapter to use at runtime
- [Strategy](../../behavioral/Strategy/) has the same structure but is about interchangeable algorithms rather than
  incompatible interfaces
