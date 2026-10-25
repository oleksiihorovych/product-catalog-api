namespace ProductCatalog.Application.Products;

public sealed class ProductCatalogService(IProductReader reader) : IProductCatalogService
{
    public IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken) =>
        reader.StreamAllAsync(cancellationToken);
}
