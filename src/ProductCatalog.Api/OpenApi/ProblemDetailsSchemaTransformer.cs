using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ProductCatalog.Api.OpenApi;

/// <summary>Documents framework error schemas without changing their response contracts.</summary>
internal sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (!typeof(ProblemDetails).IsAssignableFrom(type))
        {
            return Task.CompletedTask;
        }

        // Framework types have no application XML comments; describe their existing schema fields here.
        schema.Description = typeof(HttpValidationProblemDetails).IsAssignableFrom(type)
            ? "An HTTP error response with validation messages grouped by field or parameter."
            : "An HTTP error response.";

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Request trace identifier for correlating this error with application diagnostics."
        };

        foreach (var property in schema.Properties)
        {
            var description = property.Key switch
            {
                "type" => "A URI reference identifying the type of error.",
                "title" => "A short summary of the type of error.",
                "status" => "The HTTP status code for this response.",
                "detail" => "An explanation of this specific error.",
                "instance" => "A URI reference identifying this occurrence of the error.",
                "errors" => "Validation messages grouped by field or parameter name.",
                _ => null
            };
            if (description is not null && property.Value is OpenApiSchema field)
            {
                field.Description = description;
            }
        }

        return Task.CompletedTask;
    }
}
