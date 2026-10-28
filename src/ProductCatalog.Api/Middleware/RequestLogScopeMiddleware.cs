using System.Diagnostics;

namespace ProductCatalog.Api.Middleware;

internal sealed class RequestLogScopeMiddleware(RequestDelegate next, ILogger<RequestLogScopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using var scope = logger.BeginScope("HTTP {HttpMethod} {RequestPath}; trace ID: {TraceId}",
            context.Request.Method, context.Request.Path.Value, Activity.Current?.Id ?? context.TraceIdentifier);

        try
        {
            await next(context);
        }
        finally
        {
            if (context.RequestAborted.IsCancellationRequested)
            {
                logger.LogDebug("Request was aborted by the client");
            }
        }
    }
}
