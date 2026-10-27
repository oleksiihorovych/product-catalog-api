using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using ProductCatalog.Api.RateLimiting;

namespace ProductCatalog.UnitTests;

public sealed class ProductListingRateLimitingTests
{
    [Theory]
    [InlineData(TestConstants.JsonContentType, true, TestConstants.ProblemJsonContentType, TestConstants.RateLimitProblemJson)]
    [InlineData(TestConstants.PlainTextContentType, false, TestConstants.Utf8PlainTextContentType, TestConstants.RateLimitMessage)]
    public async Task Rejection_Preserves429WhenProblemDetailsWriterIsUnavailable(
        string accept, bool canWrite, string expectedContentType, string expectedBody)
    {
        var problemDetails = new StubProblemDetailsService(canWrite);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IProblemDetailsService>(problemDetails);
        services.AddProductListingRateLimiting(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Headers.Accept = accept;
        // RateLimitingMiddleware sets this status before invoking OnRejected.
        http.Response.StatusCode = options.RejectionStatusCode;
        await using var body = new MemoryStream();
        http.Response.Body = body;

        using var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0
        });
        using var acquired = limiter.AttemptAcquire();
        using var rejected = limiter.AttemptAcquire();

        Assert.False(rejected.IsAcquired);
        Assert.NotNull(options.OnRejected);

        await options.OnRejected(new OnRejectedContext { HttpContext = http, Lease = rejected }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status429TooManyRequests, http.Response.StatusCode);
        Assert.Equal(expectedContentType, http.Response.ContentType);
        Assert.Equal(StatusCodes.Status429TooManyRequests, problemDetails.Context?.ProblemDetails.Status);
        body.Position = 0;
        using var reader = new StreamReader(body);

        Assert.Equal(expectedBody, await reader.ReadToEndAsync());
    }

    private sealed class StubProblemDetailsService(bool canWrite) : IProblemDetailsService
    {
        public ProblemDetailsContext? Context { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context) =>
            throw new InvalidOperationException("The rejection handler must use TryWriteAsync.");

        public async ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Context = context;

            if (!canWrite)
            {
                return false;
            }

            context.HttpContext.Response.ContentType = TestConstants.ProblemJsonContentType;
            await context.HttpContext.Response.WriteAsync(TestConstants.RateLimitProblemJson);

            return true;
        }
    }
}
