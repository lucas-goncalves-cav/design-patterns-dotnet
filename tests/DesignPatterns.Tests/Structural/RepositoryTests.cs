using DesignPatterns.Structural.Repository;
using DesignPatterns.Structural.Repository.GoodExample;
using FluentAssertions;

namespace DesignPatterns.Tests.Structural;

/// <summary>
/// Every test here runs without a database. That is the measurable difference
/// the pattern buys: the bad example cannot be tested this way at all, because
/// it holds an <c>IDbConnection</c> and issues SQL directly.
/// </summary>
public class RepositoryTests
{
    private static readonly Guid ProductId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static InMemoryProductRepository SeededRepository() =>
        new(
        [
            new Product(ProductId, "Mechanical Keyboard", "Electronics", 400m, 25),
            new Product(Guid.NewGuid(), "Wireless Mouse", "Electronics", 150m, 8),
            new Product(Guid.NewGuid(), "Office Chair", "Furniture", 1_200m, 3),
            new Product(Guid.NewGuid(), "Standing Desk", "Furniture", 1_900m, 0, active: false),
            new Product(Guid.NewGuid(), "Clean Architecture", "Books", 190m, 40)
        ]);

    [Fact]
    public void ApplyDiscountLowersThePriceAndCommitsOnce()
    {
        var repository = SeededRepository();
        var service = new CatalogService(repository, repository);

        service.ApplyDiscount(ProductId, 25m);

        repository.GetById(ProductId)!.Price.Should().Be(300m);
        repository.CommitCount.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100)]
    [InlineData(150)]
    public void DiscountPercentageMustBeBetweenZeroAndOneHundred(decimal percentage)
    {
        var repository = SeededRepository();
        var service = new CatalogService(repository, repository);

        var act = () => service.ApplyDiscount(ProductId, percentage);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AnInactiveProductCannotBeDiscounted()
    {
        var repository = SeededRepository();
        var inactive = repository.Search(new ProductFilter(Active: false)).Items.Single();
        var service = new CatalogService(repository, repository);

        var act = () => service.ApplyDiscount(inactive.Id, 10m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*inactive*");
    }

    [Fact]
    public void AMissingProductIsReportedClearly()
    {
        var repository = SeededRepository();
        var service = new CatalogService(repository, repository);

        var act = () => service.ApplyDiscount(Guid.NewGuid(), 10m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*was not found*");
    }

    [Fact]
    public void NothingIsCommittedWhenTheBusinessRuleRejectsTheChange()
    {
        var repository = SeededRepository();
        var service = new CatalogService(repository, repository);

        // 400 * (1 - 0.99999) = 0.004, which rounds to 0.00.
        var act = () => service.ApplyDiscount(ProductId, 99.999m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*zero or negative*");
        repository.GetById(ProductId)!.Price.Should().Be(400m);
        repository.CommitCount.Should().Be(0);
    }

    [Fact]
    public void LowStockIsADomainQueryRatherThanAFilterBuiltByTheCaller()
    {
        var repository = SeededRepository();
        var service = new CatalogService(repository, repository);

        var toRestock = service.FindProductsToRestock(threshold: 10);

        toRestock.Select(product => product.Name)
            .Should().BeEquivalentTo(["Office Chair", "Wireless Mouse"], options => options.WithStrictOrdering());
    }

    [Fact]
    public void SearchFiltersByCategoryAndStatus()
    {
        var repository = SeededRepository();

        var result = repository.Search(new ProductFilter(Category: "Furniture", Active: true));

        result.TotalItems.Should().Be(1);
        result.Items.Single().Name.Should().Be("Office Chair");
    }

    [Fact]
    public void SearchPaginates()
    {
        var repository = SeededRepository();

        var firstPage = repository.Search(new ProductFilter(PageSize: 2, Page: 1));
        var secondPage = repository.Search(new ProductFilter(PageSize: 2, Page: 2));

        firstPage.Items.Should().HaveCount(2);
        firstPage.TotalItems.Should().Be(5);
        firstPage.TotalPages.Should().Be(3);
        secondPage.Items.Should().HaveCount(2);
        secondPage.Items.Should().NotIntersectWith(firstPage.Items);
    }

    /// <summary>
    /// Swapping the implementation is a registration change. The service does
    /// not know which one it got.
    /// </summary>
    [Fact]
    public void TheServiceWorksWithAnyImplementationOfTheContract()
    {
        var recording = new RecordingProductRepository();
        var service = new CatalogService(recording, recording);

        service.ApplyDiscount(ProductId, 50m);

        recording.Updated.Should().ContainSingle();
        recording.Updated.Single().Price.Should().Be(200m);
        recording.Commits.Should().Be(1);
    }

    private sealed class RecordingProductRepository : IProductRepository, IUnitOfWork
    {
        private readonly Product _product = new(ProductId, "Keyboard", "Electronics", 400m, 10);

        public List<Product> Updated { get; } = [];

        public int Commits { get; private set; }

        public Product? GetById(Guid id) => id == ProductId ? _product : null;

        public PagedResult<Product> Search(ProductFilter filter) => new([], 0, 1, filter.PageSize);

        public IReadOnlyCollection<Product> GetLowStock(int threshold) => [];

        public void Add(Product product) => Updated.Add(product);

        public void Update(Product product) => Updated.Add(product);

        public int Commit()
        {
            Commits++;

            return Updated.Count;
        }
    }
}
