namespace ProductCatalog.Api.Products;

/// <summary>A catalog product. Prices use two decimal places; description may be null.</summary>
/// <param name="Id">Nonempty GUID product identifier.</param>
/// <param name="Name">Product name, at most 200 characters.</param>
/// <param name="ImgUri">Absolute HTTP or HTTPS image URI, at most 2048 characters.</param>
/// <param name="Price">Nonnegative decimal(18,2) price.</param>
/// <param name="Description">Optional description, at most 4000 characters.</param>
public sealed record ProductResponse(
    Guid Id,
    string Name,
    string ImgUri,
    decimal Price,
    string? Description);
