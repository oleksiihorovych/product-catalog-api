using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductCatalog.Api.DTOs.Shared;
using ProductCatalog.Api.Mappings;
using ProductCatalog.Application.Services;

namespace ProductCatalog.Api.Controllers.GetProducts.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/v1/products")]
public sealed class GetProductsController(IProductApplicationService catalog) : ControllerBase
{
    /// <summary>Get all products.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>All products in ascending ID order, or an empty array when the catalog is empty.</returns>
    /// <remarks>
    /// Returns the complete catalog without pagination. Read the entire response;
    /// an interrupted transfer may contain an incomplete JSON array.
    /// </remarks>
    /// <response code="200">The complete product list, or an empty array.</response>
    /// <response code="429">Too many concurrent requests for the complete catalog.</response>
    /// <response code="500">An unexpected error occurred before the response started.</response>
    [HttpGet]
    [EndpointName("ListAllProductsV1")]
    [ProducesResponseType<ProductResponse[]>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [EnableRateLimiting("catalog-listing")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IAsyncEnumerable<ProductResponse> Get(CancellationToken cancellationToken) =>
        catalog.ListAllAsync(cancellationToken).ToResponses(cancellationToken);
}
