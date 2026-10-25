namespace ProductCatalog.Application.Products;

/// <summary>An immutable catalog read model; description may be null.</summary>
public sealed record ProductDetails(
    long Id,
    string Name,
    string ImgUri,
    decimal Price,
    string? Description);
