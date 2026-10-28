using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ProductCatalog.Api.DTOs.Shared;
using ProductCatalog.Domain;

namespace ProductCatalog.Api.OpenApi;

/// <summary>Describes the exact decimal price without implying binary floating-point precision.</summary>
internal sealed class ProductResponseSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(ProductResponse) &&
            schema.Properties?.TryGetValue("price", out var property) == true && property is OpenApiSchema price)
        {
            // Decimal is a tooling hint; clients must preserve decimal precision even if they ignore the format.
            price.Type = JsonSchemaType.Number;
            price.Format = "decimal";
            price.Pattern = null;
            price.Minimum = "0";
            price.Maximum = DomainConstants.ProductMaxPrice.ToString(CultureInfo.InvariantCulture);
            price.MultipleOf = 0.01m;
        }

        return Task.CompletedTask;
    }
}
