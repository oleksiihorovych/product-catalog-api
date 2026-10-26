using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Infrastructure.Persistence;

/// <summary>Reads products through untracked projections and performs targeted SQL updates.</summary>
/// <remarks>The DbContext must remain alive until enumeration completes or is disposed.</remarks>
public sealed class SqlProductRepository(CatalogDbContext db) : IProductRepository
{
    /// <inheritdoc/>
    public Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetails(p.Id, p.Name, p.ImgUri, p.Price, p.Description))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken) =>
        Stream(cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> UpdateDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken)
    {
        var affected = await db.Products.Where(product => product.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.Description, description), cancellationToken);
        return affected != 0;
    }

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
