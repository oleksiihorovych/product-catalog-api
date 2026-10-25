using System.Runtime.CompilerServices;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Api.Products;

internal static class ProductResponseMapping
{
    public static async IAsyncEnumerable<ProductResponse> ToResponses(
        this IAsyncEnumerable<ProductDetails> products,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var product in products.WithCancellation(cancellationToken))
        {
            yield return new ProductResponse(
                product.Id,
                product.Name,
                product.ImgUri,
                product.Price,
                product.Description);
        }
    }
}
