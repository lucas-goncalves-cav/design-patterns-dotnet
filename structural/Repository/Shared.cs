namespace DesignPatterns.Structural.Repository;

public sealed class Product
{
    public Product(Guid id, string name, string category, decimal price, int stock, bool active = true)
    {
        Id = id;
        Name = name;
        Category = category;
        Price = price;
        Stock = stock;
        Active = active;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public string Category { get; private set; }

    public decimal Price { get; private set; }

    public int Stock { get; private set; }

    public bool Active { get; private set; }

    public void ChangePrice(decimal price)
    {
        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be greater than zero.");
        }

        Price = price;
    }

    public void Deactivate() => Active = false;
}

public sealed record ProductFilter(
    string? Name = null,
    string? Category = null,
    bool? Active = null,
    int Page = 1,
    int PageSize = 20);

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalItems, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
