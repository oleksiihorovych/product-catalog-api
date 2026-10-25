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
}
