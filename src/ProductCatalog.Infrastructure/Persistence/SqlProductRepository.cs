using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Infrastructure.Persistence;

/// <summary>Reads products directly from SQL Server through an ordered, untracked projection.</summary>
/// <remarks>The DbContext must remain alive until enumeration completes or is disposed.</remarks>
public sealed class SqlProductRepository(CatalogDbContext db) : IProductRepository
{
    /// <inheritdoc/>
    public IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken) =>
        Stream(cancellationToken);

    private async IAsyncEnumerable<ProductDetails> Stream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = db.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new ProductDetails(p.Id, p.Name, p.ImgUri, p.Price, p.Description));

        await foreach (var product in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return product;
        }
    }
}
