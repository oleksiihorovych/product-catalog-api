using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProductCatalog.Domain;

namespace ProductCatalog.Api.DTOs.Shared;

/// <summary>Product information.</summary>
/// <param name="Id">The product identifier, as a nonempty GUID.</param>
/// <param name="Name">Product name, at most 200 characters.</param>
/// <param name="ImgUri">Absolute HTTP or HTTPS image URI, at most 2048 characters.</param>
/// <param name="Price">Exact decimal price from 0 to 9999999999999999.99, with at most two decimal places. Clients must use a decimal-capable type.</param>
/// <param name="Description">Product description, at most 4000 characters; null when absent.</param>
public sealed record ProductResponse(
    Guid Id,
    [property: MaxLength(DomainConstants.ProductMaxNameLength)] string Name,
    [property: MaxLength(DomainConstants.ProductMaxImageUriLength)] string ImgUri,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] decimal Price,
    [property: MaxLength(DomainConstants.ProductMaxDescriptionLength)] string? Description);
