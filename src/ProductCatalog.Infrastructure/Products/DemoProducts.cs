using ProductCatalog.Domain.Products;

namespace ProductCatalog.Infrastructure.Products;

public static class DemoProducts
{
    public static IReadOnlyList<Product> Create() =>
        Enumerable.Range(1, 23)
            .Select(id => new Product(
                Guid.Parse($"00000000-0000-0000-0000-{id:x12}"),
                $"Demo product {id:00}",
                $"https://example.com/products/{id}.jpg",
                id * 10.25m,
                id % 3 == 0 ? null : $"Description for demo product {id:00}."))
            .ToArray();
}
