namespace ProductCatalog.Api.DTOs.Shared;

/// <summary>Product information.</summary>
/// <param name="Id">The product identifier, as a nonempty GUID.</param>
/// <param name="Name">Product name, at most 200 characters.</param>
/// <param name="ImgUri">Absolute HTTP or HTTPS image URI, at most 2048 characters.</param>
/// <param name="Price">Nonnegative price with at most two decimal places.</param>
/// <param name="Description">Product description, at most 4000 characters; null when absent.</param>
public sealed record ProductResponse(
    Guid Id,
    string Name,
    string ImgUri,
    decimal Price,
    string? Description);
