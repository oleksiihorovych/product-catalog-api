using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Api.Products;

internal sealed class ProductsJsonResult(IAsyncEnumerable<ProductDetails> products) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext;
        var cancellationToken = http.RequestAborted;
        var json = http.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        await using var enumerator = products.GetAsyncEnumerator(cancellationToken);
        try
        {
            // Establish the query before committing 200 headers, so initial failures use Problem Details.
            var hasProduct = await enumerator.MoveNextAsync();
            http.Response.StatusCode = StatusCodes.Status200OK;
            http.Response.ContentType = "application/json; charset=utf-8";
            http.Response.Headers.CacheControl = "no-store";
            using var writer = new Utf8JsonWriter(http.Response.BodyWriter);
            writer.WriteStartArray();
            var count = 0;
            while (hasProduct)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var p = enumerator.Current;
                JsonSerializer.Serialize(writer, new ProductResponse(p.Id, p.Name, p.ImgUri, p.Price, p.Description), json);
                // Flush the first item for prompt headers, then bounded batches for backpressure.
                if (count++ % 32 == 0)
                    await FlushAsync(writer, http, cancellationToken);
                hasProduct = await enumerator.MoveNextAsync();
            }
            writer.WriteEndArray();
            await FlushAsync(writer, http, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            http.Abort();
        }
        catch (Exception exception) when (http.Response.HasStarted)
        {
            http.RequestServices.GetRequiredService<ILogger<ProductsJsonResult>>()
                .LogError(exception, "Catalog stream failed after the response started; aborting transfer");
            http.Abort();
        }
    }

    private static async Task FlushAsync(Utf8JsonWriter writer, HttpContext http, CancellationToken cancellationToken)
    {
        writer.Flush();
        var result = await http.Response.BodyWriter.FlushAsync(cancellationToken);
        if (result.IsCanceled)
            throw new OperationCanceledException(cancellationToken);
        if (result.IsCompleted)
            throw new IOException("The response transport closed during catalog streaming.");
    }
}
