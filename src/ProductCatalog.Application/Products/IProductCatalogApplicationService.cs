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
}
