using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.DTOs.ListProducts;
using ProductCatalog.Api.Mappings;
using ProductCatalog.Application.Services;

namespace ProductCatalog.Api.Controllers.GetProducts.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
[Route("api/v2/products")]
public sealed class GetProductsController(IProductApplicationService catalog) : ControllerBase
{
    /// <summary>Get a page of products.</summary>
    /// <param name="request">The page number and number of products per page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A page of products in ascending ID order, with pagination details.</returns>
    /// <remarks>
    /// A page beyond the end of the catalog contains no items and hasNext is false.
    /// Page boundaries may change if products are added or removed between requests.
    /// </remarks>
    /// <response code="200">The requested product page, which may be empty.</response>
    /// <response code="400">Pagination parameters are invalid or exceed the supported range.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet]
    [EndpointName("GetProductsV2")]
    [ProducesResponseType<ProductListResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ProductListResponse>> Get(
        [FromQuery] GetProductsRequest request, CancellationToken cancellationToken)
    {
        var page = await catalog.ListPageAsync(request.Page, request.PageSize, cancellationToken);
        return Ok(new ProductListResponse(page.Items.Select(product => product.ToResponse()).ToArray(),
            page.Page, page.PageSize, page.HasNext));
    }
}
