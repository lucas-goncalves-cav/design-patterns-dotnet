# Repository

> Not a Gang of Four pattern. It comes from Martin Fowler's *Patterns of Enterprise Application Architecture*, and it
> is the most argued about pattern in .NET. This page takes a position and explains it.

## The problem it solves

Business logic and data access end up in the same method, so neither can change or be tested without the other.

## The scenario

Applying a discount to a product: fetch it, check it is active, compute the new price, check the result is still
positive, save it.

## Bad example

[`BadExample/CatalogService.cs`](BadExample/CatalogService.cs)

```csharp
public void ApplyDiscount(Guid productId, decimal percentage)
{
    using var read = _connection.CreateCommand();
    read.CommandText = "SELECT Price, Active FROM Products WHERE Id = @Id";
    // ... read into currentPrice and active

    if (!active) throw new InvalidOperationException("An inactive product cannot be discounted.");

    var newPrice = decimal.Round(currentPrice * (1 - percentage / 100m), 2);

    if (newPrice <= 0) throw new InvalidOperationException("The discount would make the price zero or negative.");

    using var update = _connection.CreateCommand();
    update.CommandText = "UPDATE Products SET Price = @Price WHERE Id = @Id";
    // ...
}
```

Three business rules are buried between two SQL commands.

- **Untestable without a database.** Verifying "a discount cannot make the price zero" requires a running SQL Server.
- **Column names are string literals.** Renaming `Price` compiles and fails at runtime.
- **Two round trips with no transaction.** Nothing here says the read and the write belong together.
- **It gets copied.** The next service needing a product writes its own `SELECT`, slightly differently.

## Good example

[`GoodExample/`](GoodExample/)

```csharp
public void ApplyDiscount(Guid productId, decimal percentage)
{
    if (percentage is <= 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(percentage), ...);

    var product = _products.GetById(productId)
        ?? throw new InvalidOperationException($"Product {productId} was not found.");

    if (!product.Active) throw new InvalidOperationException("An inactive product cannot be discounted.");

    var newPrice = decimal.Round(product.Price * (1 - percentage / 100m), 2);

    if (newPrice <= 0) throw new InvalidOperationException("The discount would make the price zero or negative.");

    product.ChangePrice(newPrice);

    _products.Update(product);
    _unitOfWork.Commit();
}
```

Every line is now a business rule. The nine tests in
[`RepositoryTests.cs`](../../tests/DesignPatterns.Tests/Structural/RepositoryTests.cs) run in 160ms with no database.

## The two mistakes that make people hate this pattern

### 1. Leaking `IQueryable` through the interface

```csharp
// This is not a repository. It is Entity Framework with extra steps.
public interface IRepository<T>
{
    IQueryable<T> Query();
    IQueryable<T> Where(Expression<Func<T, bool>> predicate);
}
```

Returning `IQueryable` means callers compose queries, so:

- The abstraction leaks the ORM completely. You cannot swap it, which was the stated reason for the interface.
- You cannot test with a list, because `IQueryable` over `List<T>` behaves differently from `IQueryable` over a
  database. `.Where(p => p.Name.Contains(x))` works in memory and may throw when translated.
- Callers write queries the database cannot execute efficiently, and nobody owns the performance.

The interface in this example returns `Product`, `PagedResult<Product>` and `IReadOnlyCollection<Product>`. Concrete
types, fully materialized, no query left to compose.

### 2. One generic repository for every entity

```csharp
public interface IRepository<T> where T : Entity
{
    T GetById(Guid id);
    IEnumerable<T> GetAll();
    void Add(T entity);
    void Delete(T entity);
}
```

This looks like reuse and is usually a net loss:

- `GetAll()` on a table with ten million rows is a loaded gun.
- Every real query needs something the generic interface does not have, so `IProductRepository : IRepository<Product>`
  appears anyway.
- The interface says nothing about the domain. `GetLowStock(threshold)` tells you something; `GetAll().Where(...)`
  does not.

Prefer a specific interface per aggregate, with methods named after what the business asks for.

## Why `Commit` is separate

`IUnitOfWork` is a separate interface on purpose. One business operation often touches several repositories and must
commit once:

```csharp
_orders.Add(order);
_products.Update(product);
_customers.Update(customer);
_unitOfWork.Commit();      // one transaction
```

If every repository had its own `Save`, that would be three transactions and no way to roll back as a unit. With EF
Core the `DbContext` is already both, so the repository takes it and `IUnitOfWork` wraps `SaveChanges`.

## What actually changed

| | Bad | Good |
| --- | --- | --- |
| Testing a business rule | Needs a database | Needs a list |
| Test suite runtime | Seconds per test | 160ms for nine |
| Renaming a column | Runtime failure | Compile error, in one file |
| Transaction boundary | Implicit, absent | Explicit, `Commit()` |
| Query reuse | Copy the SQL | Call the method |

## When to use it

- The domain has behaviour worth testing without infrastructure
- More than one place needs the same query, and you want it defined once
- Queries express domain concepts: "products to restock", "overdue invoices"
- You want business logic to compile without a reference to the data access library

## When not to use it

This is where the argument lives, and there is a real case against it.

- **EF Core's `DbContext` is already a Unit of Work, and `DbSet<T>` is already a repository.** Wrapping them in your
  own `IRepository` that forwards every call adds a layer and removes nothing. Testing against `DbContext` with the
  SQLite in memory provider or Testcontainers is often better than testing against a fake repository, because it
  exercises the real query translation.
- **CRUD with no domain logic.** If the service is `GetById`, map, return, the repository is ceremony. Query the
  `DbContext` directly.
- **Complex reads.** A report joining six tables does not belong behind a repository method returning entities.
  Use a query object or Dapper straight to a DTO. Repository is for the write side; reads are often better served by
  something else, which is the argument CQRS makes.
- **You would only ever have one implementation.** "We might swap the database" is rarely true and is the weakest
  justification for the pattern. Testability is the strong one. Make that argument instead.

## An honest summary

The strongest reason for a repository is **a testable domain**, not database portability. Nobody swaps SQL Server for
MongoDB by changing a registration, and pretending otherwise is how teams end up with a leaky generic repository they
resent.

If the domain has real rules, a narrow repository per aggregate with domain named methods pays for itself. If the
application is CRUD over tables, use `DbContext` directly and spend the complexity budget somewhere it matters.

## Related

- [Adapter](../Adapter/) also hides an external contract, but for third party interfaces rather than storage
- [Factory](../../creational/Factory/) for choosing an implementation at runtime
- [Decorator](../Decorator/) works well over a repository, adding caching without touching the query code
