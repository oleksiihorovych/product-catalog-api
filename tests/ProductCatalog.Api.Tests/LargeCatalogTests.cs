using System.Net.Http.Json;
using ProductCatalog.Api.Products;
using Xunit;
using Xunit.Abstractions;

namespace ProductCatalog.Api.Tests;

[CollectionDefinition("Streaming memory", DisableParallelization = true)]
public sealed class StreamingMemoryCollection;

[Collection("Streaming memory")]
public sealed class LargeCatalogTests(ITestOutputHelper output)
{
    [Fact]
    public async Task LargeCatalog_IsConsumedIncrementallyWithBoundedLiveManagedMemory()
    {
        // Synthetic source; this is a regression check, not a SQL or 10k-RPS benchmark.
        await using var factory = ProductListingTests.CreateFactory(new ProductListingTests.SequenceReader(50_000));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products", HttpCompletionOption.ResponseHeadersRead);
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        var peak = baseline;
        var count = 0;
        await foreach (var product in response.Content.ReadFromJsonAsAsyncEnumerable<ProductResponse>())
        {
            Assert.NotNull(product);
            Assert.Equal(++count, product.Id);
            if (count % 5000 == 0)
                peak = Math.Max(peak, GC.GetTotalMemory(forceFullCollection: true));
        }
        Assert.Equal(50_000, count);
        output.WriteLine($"50,000 rows, 2048-character descriptions; sampled live managed heap growth: {peak - baseline:N0} bytes.");
        Assert.True(peak - baseline < 64L * 1024 * 1024, "Streaming retained more than the 64 MiB regression budget.");
    }
}
