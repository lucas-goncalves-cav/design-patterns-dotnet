# design-patterns-dotnet

Design patterns in C#, taught through the code that made them necessary.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![C#](https://img.shields.io/badge/C%23-13-239120)
![Tests](https://img.shields.io/badge/tests-96%20passing-success)
![License](https://img.shields.io/badge/license-MIT-green)

## Description

Most pattern tutorials show the solution and skip the problem, which leaves you knowing the shape of a pattern without
knowing when it earns its cost. This repository does the opposite.

Every pattern has:

- **`BadExample/`** the code you would actually write first, with the specific problems named
- **`GoodExample/`** the refactor
- **`README.md`** what changed, when to use it, and **when not to**
- **Tests** proving the two versions behave identically

The bad examples are not strawmen. They are the honest first version, and several of them are the right answer for a
small enough application. The READMEs say so where it is true.

## Objective

Be able to decide whether a pattern is worth applying, not just recite its structure.

## Patterns

### Creational

| Pattern | Scenario |
| --- | --- |
| [Factory](creational/Factory/) | Choosing a payment gateway from configuration, and getting a testable seam in return |

### Structural

| Pattern | Scenario |
| --- | --- |
| [Adapter](structural/Adapter/) | Three payment SDKs that disagree about amounts, ids and status values |
| [Decorator](structural/Decorator/) | Adding logging, caching, timing and retries to a pricing service |
| [Repository](structural/Repository/) | Separating business rules from SQL, with an honest look at the arguments against |

### Behavioral

| Pattern | Scenario |
| --- | --- |
| [Strategy](behavioral/Strategy/) | Four payment methods with different fees and settlement rules |
| [Observer](behavioral/Observer/) | One confirmed order, four independent reactions, isolated from each other |
| [Chain of Responsibility](behavioral/ChainOfResponsibility/) | Six validation steps, stopping at the first problem |
| [Mediator](behavioral/Mediator/) | A controller with six dependencies, reduced to one |

### Real world

| Example | Patterns combined |
| --- | --- |
| [Order checkout](real-world/) | Chain of Responsibility, Strategy, Observer and Factory in one operation |

## What makes this different

**The bad examples are realistic.** The Strategy bad example uses a `switch`, which is genuinely fine for two payment
methods. The README says exactly when it stops being fine: around the fourth one, or the moment a second `switch` on
the same enum appears elsewhere.

**The costs are stated.** Decorator makes stack traces worse. Mediator makes "go to definition" useless. Observer makes
control flow harder to follow. Each README has a section saying so.

**Measured claims, not asserted ones.** The Repository README says its nine tests run in 160ms without a database,
because that is the argument for the pattern and it is checkable.

**Tests prove equivalence.** Where the bad and good versions can be compared directly, a test runs both against the
same inputs and asserts they agree. A refactor that changes behaviour is not a refactor.

```csharp
[Theory]
[MemberData(nameof(EquivalentRequests))]
public void BothVersionsProduceTheSameResult(PaymentRequest request)
{
    var bad = new BadProcessor().Process(request);
    var good = CreateGoodProcessor().Process(request);

    good.Should().BeEquivalentTo(bad);
}
```

## Details worth stealing

A few implementation details in here are the kind that only surface once something is actually tested:

| Where | Detail |
| --- | --- |
| [Adapter](structural/Adapter/) | Status mappings **throw** on an unknown value instead of defaulting. A provider adding a status should be a loud failure, not a paid order silently treated as unpaid. |
| [Observer](behavioral/Observer/) | Handlers are isolated **and** failures are returned. A bare `catch {}` would be worse than the coupling it replaced. |
| [Mediator](behavioral/Mediator/) | Reflection wraps handler exceptions in `TargetInvocationException`. `ExceptionDispatchInfo` rethrows the original with its stack trace intact. |
| [Decorator](structural/Decorator/) | Composition order changes what you measure. Cache outside timing measures real work; timing outside cache measures what the caller experiences. |
| [Chain of Responsibility](behavioral/ChainOfResponsibility/) | When one step computes a value later steps need, handlers stop being independent. That coupling is made explicit rather than accidental. |

## Project structure

```
creational/          Factory
structural/          Adapter, Decorator, Repository
behavioral/          Strategy, Observer, Chain of Responsibility, Mediator
real-world/          Patterns combined in one checkout
tests/               96 tests, including bad vs good equivalence
```

Each pattern folder holds `Shared.cs` with the types both versions use, `BadExample/`, `GoodExample/` and a `README.md`.

## How to run

```bash
git clone https://github.com/lucas-goncalves-cav/design-patterns-dotnet.git
cd design-patterns-dotnet

dotnet build
dotnet test
```

Running one pattern's tests:

```bash
dotnet test --filter "FullyQualifiedName~StrategyTests"
dotnet test --filter "FullyQualifiedName~RealWorld"
```

## How to read a pattern

1. Read `BadExample/`. The problems are named in the class comment.
2. Read the `README.md` section explaining what specifically goes wrong.
3. Read `GoodExample/`.
4. Read the **When not to use it** section. It is usually the most useful part.
5. Read the tests to see the behaviour pinned down.

## A note on .NET

Several of these patterns are partly or entirely provided by the framework, and the READMEs say so rather than
pretending otherwise:

| Pattern | What .NET already gives you |
| --- | --- |
| Factory | Keyed services since .NET 8, `IServiceProvider` |
| Decorator | `DelegatingHandler`, Polly, Scrutor's `services.Decorate<,>()` |
| Observer | C# `event`, `IObservable<T>`, MediatR notifications |
| Chain of Responsibility | ASP.NET Core middleware, `IPipelineBehavior<,>` |
| Mediator | MediatR, though check its licensing before adopting |
| Repository | `DbContext` is already a Unit of Work, `DbSet<T>` already a repository |

Knowing the pattern is still worth it. Recognising it in the framework is most of the value, and writing your own
version is usually the wrong move.

## Technologies

- .NET 9, C# 13
- xUnit, FluentAssertions
- GitHub Actions

## Roadmap

- [ ] Builder, for constructing objects with many optional parameters
- [ ] Specification, for composable query rules
- [ ] State, and how it differs from Strategy
- [ ] Command, with undo
- [ ] Anti patterns: Singleton, Service Locator, and why they are on this list

## License

Distributed under the MIT License. See [LICENSE](LICENSE) for details.
