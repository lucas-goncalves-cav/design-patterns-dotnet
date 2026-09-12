namespace DesignPatterns.Structural.Repository.GoodExample;

/// <summary>
/// The same operation as the bad example, with the storage mechanism removed.
/// Every line here is a business rule.
/// </summary>
public sealed class CatalogService
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public CatalogService(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public void ApplyDiscount(Guid productId, decimal percentage)
    {
        if (percentage is <= 0 or >= 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Discount must be between 0 and 100.");
        }

        var product = _products.GetById(productId)
            ?? throw new InvalidOperationException($"Product {productId} was not found.");

        if (!product.Active)
        {
            throw new InvalidOperationException("An inactive product cannot be discounted.");
        }

        var newPrice = decimal.Round(product.Price * (1 - percentage / 100m), 2);

        if (newPrice <= 0)
        {
            throw new InvalidOperationException("The discount would make the price zero or negative.");
        }

        product.ChangePrice(newPrice);

        _products.Update(product);
        _unitOfWork.Commit();
    }

    /// <summary>
    /// A named query on the repository rather than a filter expression built
    /// by the caller. "Low stock" is a domain concept, and keeping it here
    /// means it is defined once instead of in every caller.
    /// </summary>
    public IReadOnlyCollection<Product> FindProductsToRestock(int threshold = 10) =>
        _products.GetLowStock(threshold);

    public PagedResult<Product> Search(ProductFilter filter) => _products.Search(filter);
}
