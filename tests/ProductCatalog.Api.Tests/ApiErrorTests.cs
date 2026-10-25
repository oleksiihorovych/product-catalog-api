using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProductCatalog.Api.Tests;

public sealed class ApiErrorTests
{
    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundProblemDetails()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnhandledException_ReturnsSafeProblemDetails(string environment)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureServices(services => services.AddControllers()
                    .AddApplicationPart(typeof(FailingController).Assembly));
            });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/__test/failure");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive-test-detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("/__test/failure", document.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }
}

// Registered only by the test factory, never by the application.
[ApiController]
[Route("__test/failure")]
public sealed class FailingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => throw new InvalidOperationException("sensitive-test-detail");
}
