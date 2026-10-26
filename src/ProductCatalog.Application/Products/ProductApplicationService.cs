namespace ProductCatalog.Application.Products;

/// <summary>Coordinates product use cases through the selected repository.</summary>
public sealed class ProductApplicationService(IProductRepository repository) : IProductApplicationService
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
