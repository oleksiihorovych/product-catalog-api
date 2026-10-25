namespace ProductCatalog.Application.Products;

public interface IProductCatalogService
{
    IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken);
}
