using System.Runtime.CompilerServices;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Api.Products;

internal static class ProductResponseMapping
{
    public static ProductResponse ToResponse(this ProductDetails product) =>
        new(product.Id, product.Name, product.ImgUri, product.Price, product.Description);

    public static async IAsyncEnumerable<ProductResponse> ToResponses(
        this IAsyncEnumerable<ProductDetails> products,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var product in products.WithCancellation(cancellationToken))
        {
            yield return product.ToResponse();
        }
    }
}
