using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ProductCatalog.Api.Products;

internal static class ProductListingRegistration
{
    public static IServiceCollection AddProductListing(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            if (!int.TryParse(configuration["Catalog:MaxConcurrentListings"] ?? "4", out var limit) || limit is < 1 or > 128)
                throw new InvalidOperationException("Catalog:MaxConcurrentListings must be between 1 and 128.");

            options.AddConcurrencyLimiter("catalog-listing", limiter =>
            {
                limiter.PermitLimit = limit;
                limiter.QueueLimit = 0;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many concurrent catalog listings."
                        }
                    });
            };
        });
    }
}
