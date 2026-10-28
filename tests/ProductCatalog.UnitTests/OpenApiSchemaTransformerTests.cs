using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ProductCatalog.Api.DTOs.ListProducts;
using ProductCatalog.Api.DTOs.Shared;
using ProductCatalog.Api.OpenApi;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class OpenApiSchemaTransformerTests
{
    [Fact]
    public void PaginationDefaults_MatchTheRuntimeValuesDocumentedForOmittedParameters()
    {
        var request = new GetProductsRequest();
        var properties = TypeDescriptor.GetProperties(request);

        Assert.Equal(request.Page, Assert.IsType<DefaultValueAttribute>(properties[nameof(request.Page)]!
            .Attributes[typeof(DefaultValueAttribute)]).Value);
        Assert.Equal(request.PageSize, Assert.IsType<DefaultValueAttribute>(properties[nameof(request.PageSize)]!
            .Attributes[typeof(DefaultValueAttribute)]).Value);
    }

    [Fact]
    public async Task ProductSchema_DescribesExactDecimalBoundsAndStep()
    {
        var price = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.String, Format = "double", Pattern = "old" };
        var schema = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema> { ["price"] = price } };
        using var services = new ServiceCollection().BuildServiceProvider();

        await new ProductResponseSchemaTransformer().TransformAsync(schema, Context(typeof(ProductResponse), services), default);

        // Check the emitted numeric literals too: converting the maximum through double would round it.
        using var json = JsonDocument.Parse(await schema.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1));
        var property = json.RootElement.GetProperty("properties").GetProperty("price");
        Assert.Equal("number", property.GetProperty("type").GetString());
        Assert.Equal("decimal", property.GetProperty("format").GetString());
        Assert.Equal(0m, property.GetProperty("minimum").GetDecimal());
        Assert.Equal(9999999999999999.99m, property.GetProperty("maximum").GetDecimal());
        Assert.Equal(0.01m, property.GetProperty("multipleOf").GetDecimal());
        Assert.False(property.TryGetProperty("pattern", out _));
    }

    [Theory]
    [InlineData(typeof(ProblemDetails))]
    [InlineData(typeof(ValidationProblemDetails))]
    public async Task ProblemSchema_ExposesTraceIdAndPreservesValidationErrors(Type type)
    {
        var errors = new OpenApiSchema { Type = JsonSchemaType.Object };
        var schema = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema> { ["errors"] = errors } };
        using var services = new ServiceCollection().BuildServiceProvider();

        await new ProblemDetailsSchemaTransformer().TransformAsync(schema, Context(type, services), default);

        Assert.Equal(JsonSchemaType.String, schema.Properties["traceId"].Type);
        Assert.False(string.IsNullOrWhiteSpace(schema.Properties["traceId"].Description));
        Assert.Same(errors, schema.Properties["errors"]);
    }

    [Fact]
    public async Task Transformers_DoNotApplyProductOrErrorContractsToUnrelatedTypes()
    {
        var price = new OpenApiSchema { Type = JsonSchemaType.String, Format = "custom" };
        var schema = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema> { ["price"] = price } };
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = Context(typeof(string), services);

        await new ProductResponseSchemaTransformer().TransformAsync(schema, context, default);
        await new ProblemDetailsSchemaTransformer().TransformAsync(schema, context, default);

        Assert.Equal(JsonSchemaType.String, price.Type);
        Assert.Equal("custom", price.Format);
        Assert.Single(schema.Properties);
    }

    private static OpenApiSchemaTransformerContext Context(Type type, IServiceProvider services) => new()
    {
        DocumentName = "v1",
        ParameterDescription = null,
        JsonPropertyInfo = null,
        JsonTypeInfo = JsonSerializerOptions.Web.GetTypeInfo(type),
        ApplicationServices = services
    };
}
