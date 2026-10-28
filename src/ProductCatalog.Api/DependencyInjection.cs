using System.Diagnostics;
using ProductCatalog.Api.Middleware;
using ProductCatalog.Api.OpenApi;
using ProductCatalog.Api.RateLimiting;

namespace ProductCatalog.Api;

internal static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        AddVersionedOpenApi(services, "v1");
        AddVersionedOpenApi(services, "v2");
        services.AddHealthChecks();

        services.AddApiProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddProductListingRateLimiting(configuration);

        return services;
    }

    private static void AddVersionedOpenApi(IServiceCollection services, string version)
    {
        services.AddOpenApi(version, options =>
        {
            options.AddSchemaTransformer<ProblemDetailsSchemaTransformer>();
            options.AddSchemaTransformer<ProductResponseSchemaTransformer>();
            // Each document includes only routes belonging to its URL version.
            options.ShouldInclude = description =>
                description.RelativePath?.StartsWith($"api/{version}/", StringComparison.OrdinalIgnoreCase) == true;
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "Product Catalog API";
                document.Info.Version = version;
                document.Info.Description = "A REST API for accessing and maintaining an online store's product catalog. " +
                    "It provides structured product information and supports updates to catalog data.\n\n" +
                    "The API uses URL path versioning. Select v1 or v2 by including /api/v1 or /api/v2 " +
                    "in the request URL. In Swagger, use the version selector to view the documentation " +
                    "for the version you want to use.";
                return Task.CompletedTask;
            });
        });
    }

    private static IServiceCollection AddApiProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
}
