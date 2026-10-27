using ProductCatalog.Api.Controllers.GetProductById;
using ProductCatalog.Api.Controllers.ChangeProductDescription;
using System.Text.Json.Schema;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using ProductCatalog.Api.DTOs.ChangeProductDescription;
using ProductCatalog.Api.DTOs.Shared;
using V1GetProductsController = ProductCatalog.Api.Controllers.GetProducts.V1.GetProductsController;

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
    [InlineData(typeof(GetProductByIdController), nameof(GetProductByIdController.GetById), "GET", "GetProductById")]
    [InlineData(typeof(ChangeProductDescriptionController), nameof(ChangeProductDescriptionController.ChangeDescription), "PATCH", "ChangeProductDescription")]
    public void ControllerMetadata_ExposesLookupAndUpdateOnlyInV1(
        Type controller, string action, string verb, string name)
    {
        var routes = controller.GetMethod(action)!.GetCustomAttributes(inherit: true)
            .OfType<HttpMethodAttribute>().ToArray();
        var route = Assert.Single(routes);
        Assert.Equal("api/v1/products/{id}", route.Template);
        Assert.Equal(new[] { verb }, route.HttpMethods);
        Assert.Equal(name + "V1", route.Name);
    }

    [Theory]
    [InlineData(typeof(V1GetProductsController), nameof(V1GetProductsController.Get), new[] { 200, 429, 500 })]
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
                ? controller == typeof(V1GetProductsController) ? typeof(ProductResponse[]) : typeof(ProductResponse)
                : response.StatusCode == 400 ? typeof(ValidationProblemDetails) : typeof(ProblemDetails);
            Assert.Equal(expected, response.Type);
        }
    }
}
