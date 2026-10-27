using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Models;
using ProductCatalog.Application.Services;
using ProductCatalog.Application;
using ProductCatalog.Infrastructure;

namespace ProductCatalog.UnitTests;

public sealed class MockProductRepositoryTests
{
    [Fact]
    public async Task ReadById_ReturnsSameDetailsAsListingForEveryProduct()
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();
        var count = 0;

        await foreach (var listed in repository.ReadAllAsync(CancellationToken.None))
        {
            var found = await repository.ReadByIdAsync(listed.Id, CancellationToken.None);

            Assert.Equal(listed, found);
            count++;
        }

        Assert.Equal(TestConstants.CatalogProductCount, count);
    }

    [Fact]
    public async Task ReadById_ReturnsNullForUnknownGuid()
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();

        var result = await repository.ReadByIdAsync(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    public async Task ReadById_HonorsCancellationForExistingAndMissingProducts(string id)
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.ReadByIdAsync(Guid.Parse(id), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Updated description")]
    public async Task UpdateDescription_ChangesOnlyDescriptionAndIsVisibleInLookupAndList(string? description)
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();
        var before = (await repository.ReadByIdAsync(TestConstants.ProductId, CancellationToken.None))!;

        Assert.True(await repository.UpdateDescriptionAsync(before.Id, description, CancellationToken.None));

        var expected = before with { Description = description };
        Assert.Equal(expected, await repository.ReadByIdAsync(before.Id, CancellationToken.None));
        var list = await List(repository);
        Assert.Equal(TestConstants.CatalogProductCount, list.Count);
        Assert.Equal(expected, Assert.Single(list, product => product.Id == before.Id));
        Assert.Equal(list.Select(product => product.Id).OrderBy(id => new System.Data.SqlTypes.SqlGuid(id)),
            list.Select(product => product.Id));
    }

    [Fact]
    public async Task UpdateDescription_ReturnsFalseForMissingProductWithoutChangingCatalog()
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();
        var before = await List(repository);

        Assert.False(await repository.UpdateDescriptionAsync(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            "New description", CancellationToken.None));
        Assert.Equal(before, await List(repository));
    }

    [Fact]
    public async Task UpdateDescription_AcceptsRepeatedValueAndLastWriteWins()
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();

        Assert.True(await repository.UpdateDescriptionAsync(TestConstants.ProductId, "First", CancellationToken.None));
        Assert.True(await repository.UpdateDescriptionAsync(TestConstants.ProductId, "First", CancellationToken.None));
        Assert.True(await repository.UpdateDescriptionAsync(TestConstants.ProductId, "Last", CancellationToken.None));
        Assert.Equal("Last", (await repository.ReadByIdAsync(TestConstants.ProductId, CancellationToken.None))!.Description);
    }

    [Fact]
    public async Task UpdateDescription_CancellationLeavesDataUnchanged()
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();
        var before = await repository.ReadByIdAsync(TestConstants.ProductId, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.UpdateDescriptionAsync(TestConstants.ProductId, "Cancelled", cancellation.Token));

        Assert.Equal(before, await repository.ReadByIdAsync(TestConstants.ProductId, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateDescription_IsSharedAcrossScopesAndResetsWithNewStore()
    {
        using var provider = CreateProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var service = firstScope.ServiceProvider.GetRequiredService<IProductApplicationService>();
        var reader = secondScope.ServiceProvider.GetRequiredService<IProductApplicationService>();
        var before = await reader.GetByIdAsync(TestConstants.ProductId, CancellationToken.None);

        Assert.True(await service.ChangeDescriptionAsync(TestConstants.ProductId, "Shared update", CancellationToken.None));
        Assert.NotSame(service, reader);
        Assert.Same(service, firstScope.ServiceProvider.GetRequiredService<IProductApplicationService>());
        Assert.Equal("Shared update", (await reader.GetByIdAsync(TestConstants.ProductId, CancellationToken.None))!.Description);

        using var freshProvider = CreateProvider();
        Assert.Equal(before, await freshProvider.GetRequiredService<IProductRepository>()
            .ReadByIdAsync(TestConstants.ProductId, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateDescription_DoesNotChangeAnExistingEnumerationSnapshot()
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IProductRepository>();
        var id = TestConstants.CatalogProductId(2);
        var before = await repository.ReadByIdAsync(id, CancellationToken.None);
        await using var enumeration = repository.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await enumeration.MoveNextAsync());

        Assert.True(await repository.UpdateDescriptionAsync(id, "New snapshot", CancellationToken.None));

        Assert.True(await enumeration.MoveNextAsync());
        Assert.Equal(before, enumeration.Current);
        Assert.Equal("New snapshot", (await repository.ReadByIdAsync(id, CancellationToken.None))!.Description);
    }

    private static async Task<List<ProductDetails>> List(IProductRepository repository)
    {
        var list = new List<ProductDetails>();
        await foreach (var product in repository.ReadAllAsync(CancellationToken.None)) list.Add(product);
        return list;
    }

    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Storage:Mode"] = "Mock" }).Build();
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
