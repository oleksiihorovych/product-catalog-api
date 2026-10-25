using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Application.Products;
using ProductCatalog.Infrastructure;
using Xunit;

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

    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Storage:Mode"] = "Mock" }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
