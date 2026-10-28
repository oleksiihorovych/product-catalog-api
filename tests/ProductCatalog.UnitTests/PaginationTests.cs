using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ProductCatalog.Api.DTOs.ListProducts;
using ProductCatalog.Api.DTOs.Shared;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Models;
using ProductCatalog.Application.Services;
using ProductCatalog.Application.Validation;
using ProductCatalog.Application;
using ProductCatalog.Infrastructure;
using V2GetProductsController = ProductCatalog.Api.Controllers.GetProducts.V2.GetProductsController;

namespace ProductCatalog.UnitTests;

public sealed class PaginationTests
{
    [Theory]
    [InlineData(1, 10, 10, true)]
    [InlineData(2, 10, 10, true)]
    [InlineData(3, 10, 3, false)]
    [InlineData(4, 10, 0, false)]
    [InlineData(1, 1, 1, true)]
    [InlineData(23, 1, 1, false)]
    [InlineData(24, 1, 0, false)]
    [InlineData(1, 23, 23, false)]
    [InlineData(1, 22, 22, true)]
    [InlineData(1, 100, 23, false)]
    public async Task ReadPage_ReturnsRequestedWindowInCatalogOrder(int page, int size, int count, bool hasNext)
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();
        var all = new List<ProductDetails>();
        await foreach (var product in repository.ReadAllAsync(CancellationToken.None)) all.Add(product);

        var result = await repository.ReadPageAsync(page, size, CancellationToken.None);

        Assert.Equal(count, result.Items.Count);
        Assert.Equal(hasNext, result.HasNext);
        Assert.Equal(page, result.Page);
        Assert.Equal(size, result.PageSize);
        Assert.Equal(all.Skip((page - 1) * size).Take(size), result.Items);
        Assert.Equal(all.Select(product => product.Id).OrderBy(id => new System.Data.SqlTypes.SqlGuid(id)),
            all.Select(product => product.Id));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task ListPage_RejectsInvalidParametersBeforeCallingRepository(int page, int size)
    {
        var repository = new StubRepository();
        var service = new ProductApplicationService(repository, NullLogger<ProductApplicationService>.Instance);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListPageAsync(page, size, CancellationToken.None));
        Assert.Equal(0, repository.Calls);
        var request = new GetProductsRequest { Page = page, PageSize = size };
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public void GetOffset_AcceptsLargestSupportedOffsetWithoutOverflow()
    {
        Assert.Equal(int.MaxValue - 1, ProductPagination.GetOffset(1073741824, 2));
        Assert.Equal(int.MaxValue - 1, ProductPagination.GetOffset(int.MaxValue, 1));
    }

    [Fact]
    public async Task ListPage_ForwardsArgumentsAndReturnsEmptyRepositoryPage()
    {
        var repository = new StubRepository();
        var service = new ProductApplicationService(repository, NullLogger<ProductApplicationService>.Instance);
        using var cancellation = new CancellationTokenSource();
        var result = await service.ListPageAsync(2, 100, cancellation.Token);
        Assert.Same(repository.Result, result);
        Assert.Empty(result.Items);
        Assert.False(result.HasNext);
        Assert.Equal((2, 100, cancellation.Token), repository.Arguments);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task ListPage_PropagatesRepositoryFailureAndCancellation()
    {
        var failure = new InvalidOperationException("Storage failed");
        var repository = new StubRepository { Failure = failure };
        var service = new ProductApplicationService(repository, NullLogger<ProductApplicationService>.Instance);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ListPageAsync(1, 10, CancellationToken.None)));
        repository.Failure = new OperationCanceledException();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListPageAsync(1, 10, CancellationToken.None));
    }

    [Fact]
    public async Task ListPage_RejectsCancelledRequestBeforeCallingRepository()
    {
        var repository = new StubRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ProductApplicationService(repository, NullLogger<ProductApplicationService>.Instance).ListPageAsync(1, 10, cancellation.Token));
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ReadPage_HonorsCancellation()
    {
        using var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IProductRepository>()
            .ReadPageAsync(1, 10, cancellation.Token));
    }

    [Fact]
    public async Task Get_UsesDefaultsAndMapsProductFields()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductApplicationService>();
        var controller = new V2GetProductsController(service);
        var result = await controller.Get(new GetProductsRequest(), CancellationToken.None);
        var response = Assert.IsType<ProductListResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var expected = await service.ListPageAsync(1, 10, CancellationToken.None);
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
        Assert.True(response.HasNext);
        Assert.Equal(expected.Items.Select(product => new ProductResponse(product.Id, product.Name,
            product.ImgUri, product.Price, product.Description)), response.Items);
    }

    [Fact]
    public async Task ReadPage_SeesUpdatesAcrossScopesWithoutMutatingPreviousPage()
    {
        using var provider = CreateProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var reader = first.ServiceProvider.GetRequiredService<IProductApplicationService>();
        var writer = second.ServiceProvider.GetRequiredService<IProductApplicationService>();
        var before = await reader.ListPageAsync(1, 10, CancellationToken.None);
        var original = before.Items[0];
        Assert.True(await writer.ChangeDescriptionAsync(original.Id, "Updated page", CancellationToken.None));
        var after = await reader.ListPageAsync(1, 10, CancellationToken.None);
        Assert.Equal(original, before.Items[0]);
        Assert.Equal(original with { Description = "Updated page" }, after.Items[0]);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Storage:Mode"] = "Mock" }).Build());
        return services.BuildServiceProvider();
    }

    private sealed class StubRepository : IProductRepository
    {
        public int Calls { get; private set; }
        public (int, int, CancellationToken) Arguments { get; private set; }
        public ProductPage Result { get; } = new([], 2, 100, false);
        public Exception? Failure { get; set; }
        public Task<ProductPage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            Calls++;
            Arguments = (page, pageSize, cancellationToken);
            return Failure is null ? Task.FromResult(Result) : Task.FromException<ProductPage>(Failure);
        }
        public IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> UpdateDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
