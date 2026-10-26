namespace ProductCatalog.Application.Products;

/// <summary>Exposes product use cases independently of HTTP and storage implementations.</summary>
public interface IProductApplicationService
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

    /// <summary>Validates the change and performs one targeted save without loading the product.</summary>
    /// <param name="id">The nonempty product identifier.</param>
    /// <param name="description">The exact new value; null clears it and whitespace is preserved.</param>
    /// <param name="cancellationToken">Cancels the operation and underlying storage call.</param>
    /// <returns>True after a successful save; false when the product does not exist.</returns>
    /// <remarks>The last successful write determines the description; other product fields remain unchanged.</remarks>
    /// <exception cref="ArgumentException">The identifier is empty.</exception>
    /// <exception cref="FluentValidation.ValidationException">The description exceeds the domain length limit.</exception>
    Task<bool> ChangeDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken);
}
