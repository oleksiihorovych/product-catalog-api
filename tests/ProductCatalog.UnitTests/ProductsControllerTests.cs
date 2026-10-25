using System.Runtime.CompilerServices;
using ProductCatalog.Api.Products;
using ProductCatalog.Application.Products;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class ProductsControllerTests
{
    [Theory]
    [InlineData(TestConstants.EmptyCatalogCount)]
    [InlineData(TestConstants.CatalogProductCount)]
    public async Task GetAll_ReturnsCompleteMappedServiceOutput(int count)
    {
        var service = new StubCatalogService(count);
        var controller = new ProductsController(service);
        using var cancellation = new CancellationTokenSource();
        var actual = new List<ProductResponse>();

        await foreach (var response in controller.GetAll(cancellation.Token))
        {
            actual.Add(response);
        }

        Assert.Equal(cancellation.Token, service.ReceivedToken);
        Assert.Equal(Enumerable.Range(1, count).Select(id =>
            new ProductResponse(id, $"Product {id}", TestConstants.CollectionImageUri, TestConstants.ProductPrice, TestConstants.ProductDescription)), actual);
    }

    private sealed class StubCatalogService(int count) : IProductCatalogApplicationService
    {
        public CancellationToken ReceivedToken { get; private set; }

        public IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;

            return Enumerate(cancellationToken);
        }

        private async IAsyncEnumerable<ProductDetails> Enumerate(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            for (var id = 1; id <= count; id++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return new ProductDetails(id, $"Product {id}", TestConstants.CollectionImageUri, TestConstants.ProductPrice, TestConstants.ProductDescription);
            }
        }
    }
}
