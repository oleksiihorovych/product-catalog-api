using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ProductCatalog.Api.RateLimiting;

internal static class ProductListingRateLimiting
{
    public static IServiceCollection AddProductListingRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            // These limits are chosen for demonstration purposes. Meaningful production limits
            // require real measurements of traffic, request duration and resource utilization.
            if (!int.TryParse(configuration["Catalog:MaxConcurrentListings"] ?? "4", out var limit) || limit is < 1 or > 128)
            {
                throw new InvalidOperationException("Catalog:MaxConcurrentListings must be between 1 and 128.");
            }

            options.AddConcurrencyLimiter("catalog-listing", limiter =>
            {
                limiter.PermitLimit = limit;
                limiter.QueueLimit = 0;
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var written = await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .TryWriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many concurrent catalog listings."
                        }
                    });

                if (!written)
                {
                    context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                    await context.HttpContext.Response.WriteAsync(
                        "Too many concurrent catalog listings.", cancellationToken);
                }
            };
        });
    }
}
