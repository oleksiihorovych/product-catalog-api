using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ProductCatalog.Api.Tests;

public sealed class HostStartupTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Liveness_ReturnsHealthyWithoutExternalServices(string environment)
    {
        await using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
    }

    [Fact]
    public async Task Development_ExposesSwaggerUiAndGeneratedOpenApi()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var ui = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Equal("text/html", ui.Content.Headers.ContentType?.MediaType);
        var html = await ui.Content.ReadAsStringAsync();
        Assert.Contains("/openapi/v1.json", html, StringComparison.Ordinal);

        using var schema = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, schema.StatusCode);
        var document = await schema.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("3.", document.GetProperty("openapi").GetString());
        Assert.True(document.TryGetProperty("info", out _));
        var operation = document.GetProperty("paths").GetProperty("/api/v1/products").GetProperty("get");
        Assert.Equal("ListAllProductsV1", operation.GetProperty("operationId").GetString());
        Assert.Equal("array", operation.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Production_DoesNotExposeDevelopmentDocumentation(string path)
    {
        await using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Application_DoesNotExposeTestOnlyController()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/__test/failure");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment(environment));
}
