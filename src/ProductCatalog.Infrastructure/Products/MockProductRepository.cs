using System.Runtime.CompilerServices;
using System.Data.SqlTypes;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Infrastructure.Products;

internal sealed class MockProductRepository : IProductRepository
{
    private readonly IReadOnlyList<ProductDetails> products = DemoProducts.Create()
        .Select(p => new ProductDetails(p.Id, p.Name, p.ImgUri, p.Price, p.Description))
        .OrderBy(p => new SqlGuid(p.Id))
        .ToArray();

    /// <inheritdoc/>
    public Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(products.SingleOrDefault(product => product.Id == id));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ProductDetails> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        foreach (var product in products)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return product;
        }
    }
}
