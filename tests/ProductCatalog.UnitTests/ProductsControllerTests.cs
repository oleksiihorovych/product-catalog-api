using ProductCatalog.Api.Validation;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        var controller = new GetAllProductsController(service);
        using var cancellation = new CancellationTokenSource();
        var actual = new List<ProductResponse>();

        await foreach (var response in controller.GetAll(cancellation.Token))
        {
            actual.Add(response);
        }

        Assert.Equal(cancellation.Token, service.ReceivedToken);
        Assert.Equal(Enumerable.Range(1, count).Select(id =>
            new ProductResponse(TestConstants.CatalogProductId(id), $"Product {id}", TestConstants.CollectionImageUri, TestConstants.ProductPrice, TestConstants.ProductDescription)), actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TestConstants.ProductDescription)]
    public async Task GetById_ReturnsMappedProductAndForwardsArguments(string? description)
    {
        var product = new ProductDetails(TestConstants.ProductId, "Product", TestConstants.CollectionImageUri, TestConstants.ProductPrice, description);
        var service = new StubCatalogService(0) { Product = product };
        var controller = new GetProductByIdController(service);
        using var cancellation = new CancellationTokenSource();

        var result = await controller.GetById(product.Id, cancellation.Token);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(new ProductResponse(product.Id, product.Name, product.ImgUri, product.Price, description), ok.Value);
        Assert.Equal(product.Id, service.ReceivedId);
        Assert.Equal(cancellation.Token, service.ReceivedToken);
        Assert.Equal(1, service.LookupCalls);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundProblemForMissingProduct()
    {
        var service = new StubCatalogService(0);
        var controller = new GetProductByIdController(service);

        var result = await controller.GetById(TestConstants.ProductId, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemDetails>(response.Value).Status);
        Assert.Equal(1, service.LookupCalls);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("00000000-0000-0000-0000-000000000001", true)]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff", true)]
    public void GetById_IdValidationAcceptsOnlyNonemptyGuids(string value, bool expected)
    {
        var parameter = typeof(GetProductByIdController)
            .GetMethod(nameof(GetProductByIdController.GetById))!
            .GetParameters()[0];
        var validation = parameter.GetCustomAttribute<NotEmptyGuidAttribute>();

        Assert.NotNull(validation);
        Assert.Equal(expected, validation.IsValid(Guid.Parse(value)));
    }

    private sealed class StubCatalogService(int count) : IProductCatalogApplicationService
    {
        public ProductDetails? Product { get; init; }

        public Guid? ReceivedId { get; private set; }

        public int LookupCalls { get; private set; }

        public Task<ProductDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            ReceivedId = id;
            ReceivedToken = cancellationToken;
            LookupCalls++;
            return Task.FromResult(Product);
        }

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

                yield return new ProductDetails(TestConstants.CatalogProductId(id), $"Product {id}", TestConstants.CollectionImageUri, TestConstants.ProductPrice, TestConstants.ProductDescription);
            }
        }
    }
}
