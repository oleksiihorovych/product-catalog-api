namespace ProductCatalog.Application.Products;

/// <summary>Provides persistence operations for catalog products.</summary>
public interface IProductRepository
{
    /// <summary>Reads every available product in ascending ID order without pagination.</summary>
    /// <param name="cancellationToken">Cancels enumeration and underlying storage operations.</param>
    /// <returns>A lazy sequence of product details, or an empty sequence for an empty catalog.</returns>
    /// <remarks>
    /// Enumerate within the repository's scope and dispose the enumerator when stopping early.
    /// Storage failures may occur during enumeration, after some products have been read.
    /// </remarks>
    IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>Reads one product by its nonempty GUID identifier.</summary>
    /// <param name="id">The nonempty product GUID, validated by the caller.</param>
    /// <param name="cancellationToken">Cancels the underlying storage operation.</param>
    /// <returns>The product details, or null when no product has that identifier.</returns>
    Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken);
}
