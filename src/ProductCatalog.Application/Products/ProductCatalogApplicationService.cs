namespace ProductCatalog.Application.Products;

/// <summary>Coordinates catalog reads through the selected repository.</summary>
public sealed class ProductCatalogApplicationService(IProductRepository repository) : IProductCatalogApplicationService
{
    /// <inheritdoc/>
    public IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken) =>
        repository.ReadAllAsync(cancellationToken);
}
