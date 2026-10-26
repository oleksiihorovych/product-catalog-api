using System.Text.Json;
using System.Text.Json.Schema;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.Products;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class DescriptionOpenApiTests
{
    [Fact]
    public void RequestSchema_IsGeneratedFromRequiredNullableDtoAndUnmappedMemberPolicy()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Web);

        var schema = options.GetJsonSchemaAsNode(typeof(ChangeProductDescriptionRequest));

        Assert.Equal("description", schema["required"]![0]!.GetValue<string>());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        var types = schema["properties"]!["description"]!["type"]!.AsArray()
            .Select(type => type!.GetValue<string>()).ToArray();
        Assert.Contains("string", types);
        Assert.Contains("null", types);
    }

    [Theory]
    [InlineData(typeof(GetAllProductsController), nameof(GetAllProductsController.GetAll), new[] { 200, 429, 500 })]
    [InlineData(typeof(GetProductByIdController), nameof(GetProductByIdController.GetById), new[] { 200, 400, 404, 500 })]
    [InlineData(typeof(ChangeProductDescriptionController), nameof(ChangeProductDescriptionController.ChangeDescription), new[] { 204, 400, 404, 415, 500 })]
    public void ControllerMetadata_DeclaresExistingResponseContract(Type controller, string action, int[] expectedCodes)
    {
        var responses = controller.GetMethod(action)!.GetCustomAttributes(inherit: true)
            .OfType<ProducesResponseTypeAttribute>().ToArray();

        Assert.Equal(expectedCodes, responses.Select(response => response.StatusCode).Order());
        foreach (var response in responses)
        {
            var expected = response.StatusCode == 204 ? typeof(void) : response.StatusCode == 200
                ? controller == typeof(GetAllProductsController) ? typeof(ProductResponse[]) : typeof(ProductResponse)
                : response.StatusCode == 400 ? typeof(ValidationProblemDetails) : typeof(ProblemDetails);
            Assert.Equal(expected, response.Type);
        }
    }
}
