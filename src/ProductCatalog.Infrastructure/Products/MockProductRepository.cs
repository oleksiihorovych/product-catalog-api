using System.Runtime.CompilerServices;
using System.Data.SqlTypes;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Infrastructure.Products;

internal sealed class MockProductRepository : IProductRepository
{
    private readonly object sync = new();
    private ProductDetails[] products = DemoProducts.Create()
        .Select(p => new ProductDetails(p.Id, p.Name, p.ImgUri, p.Price, p.Description))
        .OrderBy(p => new SqlGuid(p.Id))
        .ToArray();

    /// <inheritdoc/>
    public Task<ProductPage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var offset = ProductPagination.GetOffset(page, pageSize);
        cancellationToken.ThrowIfCancellationRequested();
        ProductDetails[] snapshot;
        lock (sync)
        {
            snapshot = products;
        }

        var window = snapshot.Skip(offset).Take(pageSize + 1).ToArray();
        return Task.FromResult(new ProductPage(window.Take(pageSize).ToArray(), page, pageSize, window.Length > pageSize));
    }

    /// <inheritdoc/>
    public Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            return Task.FromResult(products.SingleOrDefault(product => product.Id == id));
        }
    }

    /// <inheritdoc/>
    public Task<bool> UpdateDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = Array.FindIndex(products, product => product.Id == id);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            // Copy the small demo catalog so readers can enumerate a stable snapshot without holding the lock.
            var updated = (ProductDetails[])products.Clone();
            updated[index] = products[index] with { Description = description };
            products = updated;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ProductDetails> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        ProductDetails[] snapshot;
        lock (sync)
        {
            snapshot = products;
        }

        foreach (var product in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return product;
        }
    }
}
