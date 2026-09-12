namespace DesignPatterns.Structural.Repository.GoodExample;

/// <summary>
/// An in memory implementation, useful for tests and for running the sample
/// without a database.
///
/// A real application would also have an EF Core or Dapper implementation of
/// the same interface. The business layer cannot tell them apart, which is the
/// whole point.
/// </summary>
public sealed class InMemoryProductRepository : IProductRepository, IUnitOfWork
{
    private readonly Dictionary<Guid, Product> _products = [];
    private readonly List<Product> _pending = [];

    public InMemoryProductRepository(IEnumerable<Product>? seed = null)
    {
        foreach (var product in seed ?? [])
        {
            _products[product.Id] = product;
        }
    }

    public int CommitCount { get; private set; }

    public Product? GetById(Guid id) => _products.GetValueOrDefault(id);

    public PagedResult<Product> Search(ProductFilter filter)
    {
        var query = _products.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            query = query.Where(product =>
                product.Name.Contains(filter.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(product =>
                product.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Active.HasValue)
        {
            query = query.Where(product => product.Active == filter.Active.Value);
        }

        var matching = query.OrderBy(product => product.Name).ToList();
        var pageSize = Math.Max(1, filter.PageSize);
        var page = Math.Max(1, filter.Page);

        var items = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<Product>(items, matching.Count, page, pageSize);
    }

    public IReadOnlyCollection<Product> GetLowStock(int threshold) =>
        _products.Values
            .Where(product => product.Active && product.Stock <= threshold)
            .OrderBy(product => product.Stock)
            .ToList();

    public void Add(Product product) => _pending.Add(product);

    public void Update(Product product) => _pending.Add(product);

    public int Commit()
    {
        foreach (var product in _pending)
        {
            _products[product.Id] = product;
        }

        var affected = _pending.Count;

        _pending.Clear();
        CommitCount++;

        return affected;
    }
}
