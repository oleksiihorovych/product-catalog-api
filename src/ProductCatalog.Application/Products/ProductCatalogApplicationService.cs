namespace ProductCatalog.Application.Products;

/// <summary>Coordinates catalog reads through the selected repository.</summary>
public sealed class ProductCatalogApplicationService(IProductRepository repository) : IProductCatalogApplicationService
{
    /// <inheritdoc/>
    public IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken) =>
        repository.ReadAllAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<ProductDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The product ID must not be empty.", nameof(id));
        }

        return repository.ReadByIdAsync(id, cancellationToken);
    }
}
