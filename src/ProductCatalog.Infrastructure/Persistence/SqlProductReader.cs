using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Infrastructure.Persistence;

public sealed class SqlProductReader(CatalogDbContext db) : IProductReader
{
    public IAsyncEnumerable<ProductDetails> StreamAllAsync(CancellationToken cancellationToken) =>
        Stream(cancellationToken);

    private async IAsyncEnumerable<ProductDetails> Stream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Do not enable retry-on-failure here: EF would buffer the complete result.
        var query = db.Products.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new ProductDetails(p.Id, p.Name, p.ImgUri, p.Price, p.Description));
        await foreach (var product in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return product;
    }
}
