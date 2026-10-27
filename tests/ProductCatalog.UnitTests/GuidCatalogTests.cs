using System.Data.SqlTypes;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ProductCatalog.Api.DTOs.Shared;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Models;
using ProductCatalog.Infrastructure.Seeding;
using ProductCatalog.Infrastructure;

namespace ProductCatalog.UnitTests;

public sealed class GuidCatalogTests
{
    [Fact]
    public void DemoProducts_HaveRepeatableUniqueNonemptyGuidIds()
    {
        var first = DemoProducts.Create();
        var second = DemoProducts.Create();

        Assert.Equal(23, first.Count);
        Assert.Equal(first.Select(p => p.Id), second.Select(p => p.Id));
        Assert.Equal(23, first.Select(p => p.Id).Distinct().Count());
        Assert.All(first, product => Assert.NotEqual(Guid.Empty, product.Id));
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000017"), first[^1].Id);
        Assert.Equal(235.75m, first[^1].Price);
    }

    [Fact]
    public async Task MockListing_ReturnsEverySeedProductInSqlGuidOrder()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var repository = provider.GetRequiredService<IProductRepository>();
        var actual = new List<ProductDetails>();

        await foreach (var product in repository.ReadAllAsync(CancellationToken.None))
        {
            actual.Add(product);
        }

        var expected = DemoProducts.Create().OrderBy(p => new SqlGuid(p.Id))
            .Select(p => new ProductDetails(p.Id, p.Name, p.ImgUri, p.Price, p.Description));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ProductResponse_SerializesIdAsUuidString()
    {
        var response = new ProductResponse(TestConstants.ProductId, TestConstants.ProductName,
            TestConstants.ProductImageUri, TestConstants.ProductPrice, null);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonSerializerOptions.Web));

        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("id").ValueKind);
        Assert.Equal(TestConstants.ProductId, document.RootElement.GetProperty("id").GetGuid());
    }
}
