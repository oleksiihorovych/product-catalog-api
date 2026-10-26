using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Api.Products;

[ApiController]
[Route("api/v1/products")]
public sealed class GetAllProductsController(IProductApplicationService catalog) : ControllerBase
{
    /// <summary>Get every product in the catalog.</summary>
    /// <param name="cancellationToken">Cancels the catalog read when the HTTP request is aborted.</param>
    /// <returns>The complete catalog as a streamed JSON array; an empty catalog produces [].</returns>
    /// <remarks>
    /// Ordered by ID with no pagination or hidden limit. Clients must consume the entire response;
    /// a failure after transmission starts cannot be replaced by a Problem Details document.
    /// Saturated admission returns 429, with a plain-text fallback if Problem Details cannot be written.
    /// No brokers are required.
    /// </remarks>
    /// <response code="200">The complete catalog, or an empty array.</response>
    /// <response code="429">The concurrent listing limit has been reached.</response>
    /// <response code="500">An unexpected failure occurred before the response started.</response>
    [HttpGet]
    [EndpointName("ListAllProductsV1")]
    [ProducesResponseType<ProductResponse[]>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [EnableRateLimiting("catalog-listing")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IAsyncEnumerable<ProductResponse> GetAll(CancellationToken cancellationToken) =>
        catalog.ListAllAsync(cancellationToken).ToResponses(cancellationToken);
}
