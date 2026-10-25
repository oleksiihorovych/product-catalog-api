using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Api.Products;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(IProductCatalogService catalog) : ControllerBase
{
    /// <summary>Get every product in the catalog.</summary>
    /// <remarks>Returns a single JSON array ordered by ID, with no pagination or hidden limit.
    /// An empty catalog returns []. The response streams; a failure after it starts aborts the transfer.</remarks>
    [HttpGet]
    [EndpointName("ListAllProductsV1")]
    [EndpointSummary("Get all products")]
    [EndpointDescription("Returns every product ordered by ID as one JSON array. Empty catalogs return []. No brokers are required.")]
    [ProducesResponseType<ProductResponse[]>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [EnableRateLimiting("catalog-listing")]
    public IActionResult GetAll(CancellationToken cancellationToken) =>
        new ProductsJsonResult(catalog.ListAllAsync(cancellationToken));
}
