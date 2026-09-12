namespace DesignPatterns.Structural.Repository.GoodExample;

/// <summary>
/// The contract the business layer depends on, expressed in domain terms.
///
/// Note what is NOT here: no <c>IQueryable</c>, no <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c>,
/// no <c>SaveChanges</c> on the repository itself. Those turn a repository
/// into a thin wrapper that leaks the ORM through the abstraction, which is
/// the most common way this pattern is implemented badly.
/// </summary>
public interface IProductRepository
{
    Product? GetById(Guid id);

    PagedResult<Product> Search(ProductFilter filter);

    IReadOnlyCollection<Product> GetLowStock(int threshold);

    void Add(Product product);

    void Update(Product product);
}

/// <summary>
/// Committing is a separate concern from reading and writing, because a single
/// business operation often touches several repositories and must commit once.
/// </summary>
public interface IUnitOfWork
{
    int Commit();
}
