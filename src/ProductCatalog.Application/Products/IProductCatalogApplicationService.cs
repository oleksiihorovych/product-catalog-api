namespace ProductCatalog.Application.Products;

/// <summary>Exposes catalog use cases independently of HTTP and storage implementations.</summary>
public interface IProductCatalogApplicationService
{
    /// <summary>Lists the complete catalog in ascending ID order.</summary>
    /// <param name="cancellationToken">Cancels enumeration and the underlying read.</param>
    /// <returns>A lazy sequence containing all products, or an empty sequence when none exist.</returns>
    /// <remarks>
    /// Does not paginate or materialize the catalog. The caller must consume the sequence
    /// within the service scope; failures can be raised while enumerating it.
    /// </remarks>
    IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>Gets one product by its nonempty GUID identifier.</summary>
    /// <param name="id">The nonempty product GUID.</param>
    /// <param name="cancellationToken">Cancels the underlying read.</param>
    /// <returns>The product details, or null when no product has that identifier.</returns>
    /// <exception cref="ArgumentException">The identifier is empty.</exception>
    Task<ProductDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
