namespace ProductCatalog.Application.Products;

public sealed record ProductDetails(long Id, string Name, string ImgUri, decimal Price, string? Description);
