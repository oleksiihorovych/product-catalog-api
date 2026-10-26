using System.Diagnostics;
using ProductCatalog.Api.Middleware;
using ProductCatalog.Api.Products;

namespace ProductCatalog.Api;

internal static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddOpenApi("v1");
        services.AddOpenApi("v2");
        services.AddHealthChecks();

        services.AddApiProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddProductListingRateLimiting(configuration);

        return services;
    }

    private static IServiceCollection AddApiProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
}
