using System.Runtime.CompilerServices;
using ProductCatalog.Application.Products;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class ProductCatalogApplicationServiceTests
{
    [Theory]
    [InlineData(TestConstants.EmptyCatalogCount)]
    [InlineData(TestConstants.CatalogProductCount)]
    public async Task ListAll_ReturnsEveryRepositoryProductInOrder(int count)
    {
        var expected = Enumerable.Range(1, count)
            .Select(id => new ProductDetails(id, $"Product {id}", TestConstants.CollectionImageUri, TestConstants.ProductPrice, null))
            .ToArray();
        var repository = new StubRepository(expected);
        var service = new ProductCatalogApplicationService(repository);
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
        var service = new ProductCatalogApplicationService(new FailingRepository(exception));

        Assert.Same(exception, Assert.Throws<InvalidOperationException>(() => service.ListAllAsync(CancellationToken.None)));
    }

    private sealed class StubRepository(IEnumerable<ProductDetails> products) : IProductRepository
    {
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
        public IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken) =>
            throw exception;
    }
}
