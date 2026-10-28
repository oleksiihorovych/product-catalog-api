using ProductCatalog.Domain.Models;
using ProductCatalog.Infrastructure.Repositories;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class CatalogOrderingTests
{
    [Fact]
    public async Task MockCatalog_UsesSqlOrderAcrossPagesAndUpdatesWithMixedGuidComponents()
    {
        // These identifiers sort in opposite orders in SQL Server and Guid.CompareTo.
        var sqlFirst = Create("ffffffff-ffff-ffff-ffff-000000000001");
        var sqlSecond = Create("00000000-0000-0000-0000-000000000002");
        var repository = new MockProductRepository([sqlSecond, sqlFirst]);
        Assert.True(sqlFirst.Id.CompareTo(sqlSecond.Id) > 0);

        var listed = new List<Guid>();
        await foreach (var product in repository.ReadAllAsync(CancellationToken.None)) listed.Add(product.Id);
        Assert.Equal(new[] { sqlFirst.Id, sqlSecond.Id }, listed);

        var first = await repository.ReadPageAsync(1, 1, CancellationToken.None);
        var second = await repository.ReadPageAsync(2, 1, CancellationToken.None);
        Assert.Equal(sqlFirst.Id, Assert.Single(first.Items).Id);
        Assert.True(first.HasNext);
        Assert.Equal(sqlSecond.Id, Assert.Single(second.Items).Id);
        Assert.False(second.HasNext);

        Assert.True(await repository.UpdateDescriptionAsync(sqlFirst.Id, "Updated", CancellationToken.None));
        var updated = await repository.ReadPageAsync(1, 1, CancellationToken.None);
        Assert.Equal(sqlFirst.Id, Assert.Single(updated.Items).Id);
        Assert.Equal("Updated", updated.Items[0].Description);
        Assert.Null(first.Items[0].Description);
    }

    [Fact]
    public async Task MockCatalog_EmptyStoreReturnsEmptyListAndPage()
    {
        var repository = new MockProductRepository([]);
        await using var iterator = repository.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.False(await iterator.MoveNextAsync());
        var page = await repository.ReadPageAsync(1, 10, CancellationToken.None);
        Assert.Empty(page.Items);
        Assert.False(page.HasNext);
    }

    private static Product Create(string id) =>
        new(Guid.Parse(id), "Product", "https://example.com/image.jpg", 1m, null);
}
