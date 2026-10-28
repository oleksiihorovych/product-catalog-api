using ProductCatalog.Domain.Models;

namespace ProductCatalog.Infrastructure.Seeding;

public static class DemoProducts
{
    /// <summary>Creates repeatable demo products with descriptions identifying their storage mode.</summary>
    /// <param name="useSqlData">Use the SQL demo label instead of the in-memory demo label.</param>
    public static IReadOnlyList<Product> Create(bool useSqlData = false) =>
        Enumerable.Range(1, 23)
            .Select(id => new Product(
                Guid.Parse($"00000000-0000-0000-0000-{id:x12}"),
                $"Demo product {id:00}",
                $"https://example.com/products/{id}.jpg",
                id * 10.25m,
                $"{(useSqlData ? "SQL mock data" : "in-memory mock data")}: Description for demo product {id:00}."))
            .ToArray();
}
