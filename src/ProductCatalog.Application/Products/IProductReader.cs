namespace ProductCatalog.Application.Products;

public interface IProductReader
{
    IAsyncEnumerable<ProductDetails> StreamAllAsync(CancellationToken cancellationToken);
}
