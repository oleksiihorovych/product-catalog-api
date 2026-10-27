using ProductCatalog.Api.Controllers.GetProductById;
using ProductCatalog.Api.Controllers.ChangeProductDescription;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using ProductCatalog.Api.DTOs.ChangeProductDescription;
using ProductCatalog.Application.Models;
using ProductCatalog.Application.Services;
using V1GetProductsController = ProductCatalog.Api.Controllers.GetProducts.V1.GetProductsController;

namespace ProductCatalog.UnitTests;

public sealed class DescriptionContractTests
{
    [Theory]
    [InlineData("null", null)]
    [InlineData("\"\"", "")]
    [InlineData("\"   \"", "   ")]
    [InlineData("\"Updated\"", "Updated")]
    public void Request_PreservesExplicitDescription(string jsonValue, string? expected)
    {
        var request = JsonSerializer.Deserialize<ChangeProductDescriptionRequest>("{\"description\":" + jsonValue + "}", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(expected, request!.Description);
        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"description\":1}")]
    [InlineData("{\"description\":true}")]
    [InlineData("{\"description\":{}}")]
    [InlineData("{\"description\":[]}")]
    [InlineData("{\"description\":null,\"price\":1}")]
    public void Request_RejectsMissingUnknownAndInvalidValues(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ChangeProductDescriptionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData("{\"description\":null,\"description\":\"other\"}")]
    [InlineData("{\"description\":null,\"Description\":null}")]
    [InlineData("{\"description\":\"same\",\"description\":\"same\"}")]
    public void RequestValidation_RejectsRepeatedDescription(string json)
    {
        var request = JsonSerializer.Deserialize<ChangeProductDescriptionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ChangeProductDescriptionRequest.Description)));
    }

    [Theory]
    [InlineData("description")]
    [InlineData("Description")]
    [InlineData("DESCRIPTION")]
    public void Request_UsesStandardMvcPropertyNameMatching(string property)
    {
        var request = JsonSerializer.Deserialize<ChangeProductDescriptionRequest>(
            "{\"" + property + "\":null}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Null(request.Description);
        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public void RequestBody_RejectsNullIndependentlyOfNullableDescription()
    {
        var body = JsonSerializer.Deserialize<ChangeProductDescriptionRequest>("null");
        var parameter = typeof(ChangeProductDescriptionController)
            .GetMethod(nameof(ChangeProductDescriptionController.ChangeDescription))!.GetParameters()[1];

        Assert.Null(body);
        Assert.False(parameter.GetCustomAttribute<RequiredAttribute>()!.IsValid(body));
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void Request_ValidatesDescriptionLength(int length, bool expected)
    {
        var request = new ChangeProductDescriptionRequest { Description = new string('x', length) };
        Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("New description")]
    public async Task Update_MapsSuccessAndForwardsAllArguments(string? description)
    {
        var service = new FakeUpdateService { Result = true };
        var controller = Controller(service);
        using var cancellation = new CancellationTokenSource();

        var result = await controller.ChangeDescription(TestConstants.ProductId, new() { Description = description }, cancellation.Token);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(TestConstants.ProductId, service.Id);
        Assert.Equal(description, service.Description);
        Assert.Equal(cancellation.Token, service.Token);
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task Update_MapsMissingProductToNotFoundProblem()
    {
        var service = new FakeUpdateService();
        var controller = Controller(service);

        var result = await controller.ChangeDescription(TestConstants.ProductId, new() { Description = null }, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemDetails>(response.Value).Status);
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public void MvcDiscovery_IncludesUpdateAndReads()
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(new AssemblyPart(typeof(GetProductByIdController).Assembly));
        manager.FeatureProviders.Add(new ControllerFeatureProvider());
        var feature = new ControllerFeature();
        manager.PopulateFeature(feature);
        Assert.Contains(feature.Controllers, type => type.AsType() == typeof(GetProductByIdController));
        Assert.Contains(feature.Controllers, type => type.AsType() == typeof(V1GetProductsController));
        Assert.Contains(feature.Controllers, type => type.AsType() == typeof(ChangeProductDescriptionController));
    }

    private static ChangeProductDescriptionController Controller(FakeUpdateService service) =>
        new(service) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };

    private sealed class FakeUpdateService : IProductApplicationService
    {
        public Task<ProductPage> ListPageAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected paginated read.");

        public IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected listing during an update scenario.");

        public Task<ProductDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected lookup during an update scenario.");

        public bool Result { get; init; }
        public int Calls { get; private set; }
        public Guid Id { get; private set; }
        public string? Description { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<bool> ChangeDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken)
        {
            Calls++;
            Id = id;
            Description = description;
            Token = cancellationToken;
            return Task.FromResult(Result);
        }
    }

}
