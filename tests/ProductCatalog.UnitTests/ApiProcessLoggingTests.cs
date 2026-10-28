using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductCatalog.Api;
using ProductCatalog.Api.Middleware;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class ApiProcessLoggingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorLogAndBusinessLogsShareRequestScopeAndProblemTraceId(bool useActivity)
    {
        using var activity = useActivity ? new Activity("request").Start() : null;
        var logs = new RecordingLogProvider();
        using var factory = LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Debug).AddProvider(logs));
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { TraceIdentifier = TestConstants.TraceIdentifier, RequestServices = provider };
        context.Request.Method = HttpMethods.Patch;
        context.Request.Path = TestConstants.ProductsPath;
        context.Request.QueryString = new QueryString("?private=secret-query");
        context.Request.Headers.Authorization = "Bearer secret-token";
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var failure = new InvalidOperationException("Storage unavailable");
        var logger = factory.CreateLogger("BusinessOperation");
        var handler = new ApiExceptionHandler(factory.CreateLogger<ApiExceptionHandler>());
        var middleware = new RequestLogScopeMiddleware(async http =>
        {
            await Task.Yield();
            logger.LogInformation("Business operation");
            await handler.TryHandleAsync(http, failure, CancellationToken.None);
        }, factory.CreateLogger<RequestLogScopeMiddleware>());

        await middleware.InvokeAsync(context);
        logger.LogInformation("Outside request");

        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        var expectedTraceId = activity?.Id ?? context.TraceIdentifier;
        Assert.Equal(expectedTraceId, problem.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(3, logs.Entries.Count);
        foreach (var entry in logs.Entries.Take(2))
        {
            Assert.Equal(expectedTraceId, entry.Scope["TraceId"]);
            Assert.Equal(HttpMethods.Patch, entry.Scope["HttpMethod"]);
            Assert.Equal(TestConstants.ProductsPath, entry.Scope["RequestPath"]);
            Assert.DoesNotContain("secret", entry.Message);
            Assert.DoesNotContain("secret", string.Join(" ", entry.Scope.Values));
        }

        var error = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Same(failure, error.Exception);
        Assert.Equal(expectedTraceId, error.Properties["TraceId"]);
        Assert.Empty(logs.Entries[^1].Scope);
    }

    [Fact]
    public async Task AbortedRequest_PropagatesCancellationAndDisposesScopeWithoutErrorLog()
    {
        using var cancellation = new CancellationTokenSource();
        var logs = new RecordingLogProvider();
        using var factory = LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Debug).AddProvider(logs));
        var logger = factory.CreateLogger<RequestLogScopeMiddleware>();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var failure = new OperationCanceledException(cancellation.Token);
        var middleware = new RequestLogScopeMiddleware(_ =>
        {
            cancellation.Cancel();
            return Task.FromException(failure);
        }, logger);

        Assert.Same(failure, await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context)));
        Assert.Equal(LogLevel.Debug, Assert.Single(logs.Entries).Level);
        logger.LogDebug("Outside request");
        Assert.Empty(logs.Entries[^1].Scope);
    }

    [Fact]
    public void InvalidRequest_LogsOnlyFieldCountAndPreservesValidationResponse()
    {
        var logs = new RecordingLogProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddProvider(logs));
        services.AddApi(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        var state = new ModelStateDictionary();
        state.SetModelValue("description", "private-description", "private-description");
        state.AddModelError("description", "private-description rejected");
        var action = new ActionContext(context, new RouteData(), new ActionDescriptor(), state);

        var response = Assert.IsType<BadRequestObjectResult>(provider.GetRequiredService<IOptions<ApiBehaviorOptions>>()
            .Value.InvalidModelStateResponseFactory(action));

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.IsType<ValidationProblemDetails>(response.Value);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal(1, entry.Properties["InvalidFieldCount"]);
        Assert.DoesNotContain("private-description", entry.Message);
    }
}
