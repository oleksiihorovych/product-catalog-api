using System.Runtime.CompilerServices;
using ProductCatalog.Application.Products;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class ProductApplicationServiceTests
{
    [Theory]
    [InlineData(TestConstants.EmptyCatalogCount)]
    [InlineData(TestConstants.CatalogProductCount)]
    public async Task ListAll_ReturnsEveryRepositoryProductInOrder(int count)
    {
        var expected = Enumerable.Range(1, count)
            .Select(id => new ProductDetails(TestConstants.CatalogProductId(id), $"Product {id}", TestConstants.CollectionImageUri, TestConstants.ProductPrice, null))
            .ToArray();
        var repository = new StubRepository(expected);
        var service = new ProductApplicationService(repository);
        using var cancellation = new CancellationTokenSource();
        var actual = new List<ProductDetails>();

        await foreach (var product in service.ListAllAsync(cancellation.Token))
        {
            actual.Add(product);
        }

        Assert.Equal(expected, actual);
        Assert.Equal(cancellation.Token, repository.ReceivedToken);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public void ListAll_PropagatesRepositoryFailure()
    {
        var exception = new InvalidOperationException("storage failure");
        var service = new ProductApplicationService(new FailingRepository(exception));

        Assert.Same(exception, Assert.Throws<InvalidOperationException>(() => service.ListAllAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task GetById_ReturnsRepositoryProductAndForwardsArguments()
    {
        var expected = new ProductDetails(TestConstants.ProductId, "Product", TestConstants.CollectionImageUri, TestConstants.ProductPrice, null);
        var repository = new StubRepository([expected]);
        var service = new ProductApplicationService(repository);
        using var cancellation = new CancellationTokenSource();

        var actual = await service.GetByIdAsync(expected.Id, cancellation.Token);

        Assert.Same(expected, actual);
        Assert.Equal(expected.Id, repository.ReceivedId);
        Assert.Equal(cancellation.Token, repository.ReceivedToken);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task GetById_ReturnsNullWhenProductIsMissing()
    {
        var repository = new StubRepository([]);
        var service = new ProductApplicationService(repository);

        Assert.Null(await service.GetByIdAsync(TestConstants.ProductId, CancellationToken.None));
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task GetById_RejectsEmptyIdBeforeCallingRepository()
    {
        var repository = new StubRepository([]);
        var service = new ProductApplicationService(repository);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.GetByIdAsync(Guid.Empty, CancellationToken.None));

        Assert.Equal("id", exception.ParamName);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task GetById_PropagatesRepositoryFailure()
    {
        var exception = new InvalidOperationException("storage failure");
        var service = new ProductApplicationService(new FailingRepository(exception));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetByIdAsync(TestConstants.ProductId, CancellationToken.None));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task GetById_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new StubRepository([]);
        var service = new ProductApplicationService(repository);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetByIdAsync(TestConstants.ProductId, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(cancellation.Token, repository.ReceivedToken);
        Assert.Equal(1, repository.Calls);
    }

    private sealed class StubRepository(IEnumerable<ProductDetails> products) : IProductRepository
    {
        public Task<ProductPage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected paginated read.");

        public Task<bool> UpdateDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected write during a read scenario.");

        public Guid? ReceivedId { get; private set; }

        public Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            ReceivedId = id;
            ReceivedToken = cancellationToken;
            Calls++;

            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<ProductDetails?>(cancellationToken)
                : Task.FromResult(products.SingleOrDefault(product => product.Id == id));
        }

        public CancellationToken ReceivedToken { get; private set; }

        public int Calls { get; private set; }

        public IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            Calls++;

            return Enumerate(cancellationToken);
        }

        private async IAsyncEnumerable<ProductDetails> Enumerate(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            foreach (var product in products)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return product;
            }
        }
    }

    private sealed class FailingRepository(Exception exception) : IProductRepository
    {
        public Task<ProductPage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected paginated read.");

        public Task<bool> UpdateDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected write during a read scenario.");

        public Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromException<ProductDetails?>(exception);

        public IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken) =>
            throw exception;
    }
}
