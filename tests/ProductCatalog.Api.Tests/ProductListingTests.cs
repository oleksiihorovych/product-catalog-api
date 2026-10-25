using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductCatalog.Api.Products;
using ProductCatalog.Application.Products;
using ProductCatalog.Infrastructure.Products;
using Xunit;

namespace ProductCatalog.Api.Tests;

public sealed class ProductListingTests
{
    [Fact]
    public async Task Listing_ReturnsEveryDemoProductInOrderWithExactValues()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var products = await response.Content.ReadFromJsonAsync<ProductResponse[]>();
        Assert.NotNull(products);
        Assert.Equal(Enumerable.Range(1, 23).Select(id => (long)id), products.Select(p => p.Id));
        var expected = DemoProducts.Create().Select(p => new ProductResponse(p.Id, p.Name, p.ImgUri, p.Price, p.Description));
        Assert.Equal(expected, products);
        Assert.Null(products[2].Description);
        Assert.Equal(235.75m, products[^1].Price);
    }

    [Fact]
    public async Task EmptyCatalog_ReturnsEmptyJsonArray()
    {
        await using var factory = CreateFactory(new SequenceReader(0));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PaginationParameters_DoNotSilentlyTruncateV1()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var products = await client.GetFromJsonAsync<ProductResponse[]>("/api/v1/products?page=1&pageSize=10");
        Assert.Equal(23, products?.Length);
        Assert.Equal(23, products![^1].Id);
    }

    [Fact]
    public async Task InitialReadFailure_ReturnsSafeProblemDetails()
    {
        await using var factory = CreateFactory(new SequenceReader(0, fail: true));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-storage-error", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task LimitedListings_DoNotBlockHealthAndReturnProblemDetails()
    {
        var reader = new GatedReader();
        await using var factory = CreateFactory(reader, maxConcurrent: "1");
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        using var first = await client.GetAsync("/api/v1/products", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var second = await client.GetAsync("/api/v1/products");
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        reader.Continue.TrySetResult();
        await first.Content.ReadAsStringAsync();
        using var third = await client.GetAsync("/api/v1/products");
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
    }

    [Fact]
    public async Task HeadersAndFirstItemArriveBeforeSourceFinishes()
    {
        var reader = new GatedReader();
        await using var factory = CreateFactory(reader);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products", HttpCompletionOption.ResponseHeadersRead);
        using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[512];
        var count = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("\"id\":1", System.Text.Encoding.UTF8.GetString(buffer, 0, count));
        Assert.False(reader.Disposed.Task.IsCompleted);
        reader.Continue.TrySetResult();
        await stream.CopyToAsync(Stream.Null);
        await reader.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ClientDisconnect_CancelsSourceAndDisposesEnumerator()
    {
        var reader = new GatedReader();
        await using var factory = CreateFactory(reader);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products", HttpCompletionOption.ResponseHeadersRead);
        response.Dispose();
        await reader.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(reader.WasCancelled);
    }

    [Fact]
    public async Task FailureAfterFirstFlush_AbortsInsteadOfReturningCompleteArrayOrProblemDetails()
    {
        var reader = new GatedReader(failAfterRelease: true);
        await using var factory = CreateFactory(reader);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[512];
        var count = await stream.ReadAsync(buffer);
        var first = System.Text.Encoding.UTF8.GetString(buffer, 0, count);
        Assert.StartsWith("[", first);
        Assert.DoesNotContain("]", first);
        reader.Continue.TrySetResult();
        await Assert.ThrowsAnyAsync<IOException>(() => stream.CopyToAsync(Stream.Null));
        await reader.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    internal static WebApplicationFactory<Program> CreateFactory(IProductReader? reader = null, string maxConcurrent = "4") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Mode"] = "Mock", ["Catalog:MaxConcurrentListings"] = maxConcurrent
            }));
            if (reader is not null)
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IProductReader>();
                    services.AddSingleton(reader);
                });
        });

    internal sealed class SequenceReader(int count, bool fail = false) : IProductReader
    {
        public async IAsyncEnumerable<ProductDetails> StreamAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (fail)
                throw new InvalidOperationException("private-storage-error");
            for (var id = 1; id <= count; id++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new(id, $"Product {id}", "https://example.com/image.jpg", 1.25m, new string('x', 2048));
            }
        }
    }

    private sealed class GatedReader(bool failAfterRelease = false) : IProductReader
    {
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasCancelled { get; private set; }

        public async IAsyncEnumerable<ProductDetails> StreamAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                yield return new(1, "First", "https://example.com/1.jpg", 10.25m, null);
                await Continue.Task.WaitAsync(cancellationToken);
                if (failAfterRelease)
                    throw new InvalidOperationException("private-storage-error");
                yield return new(2, "Last", "https://example.com/2.jpg", 20.50m, null);
            }
            finally
            {
                WasCancelled = cancellationToken.IsCancellationRequested;
                Disposed.TrySetResult();
            }
        }
    }
}
