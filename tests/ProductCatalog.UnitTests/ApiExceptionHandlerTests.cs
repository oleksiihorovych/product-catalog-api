using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ProductCatalog.Api.Middleware;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsSafeProblemDetailsWithoutExceptionDetails()
    {
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = TestConstants.TraceIdentifier };
        context.Request.Path = TestConstants.ProductsPath;
        await using var body = new MemoryStream();
        context.Response.Body = body;

        var handled = await handler.TryHandleAsync(context,
            new InvalidOperationException(TestConstants.SensitiveErrorDetail), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(TestConstants.ProblemJsonContentType, context.Response.ContentType);
        body.Position = 0;
        using var reader = new StreamReader(body);
        var json = await reader.ReadToEndAsync();

        Assert.DoesNotContain(TestConstants.SensitiveErrorDetail, json);
        Assert.DoesNotContain(nameof(InvalidOperationException), json);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(StatusCodes.Status500InternalServerError, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(TestConstants.ProductsPath, document.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }
}
