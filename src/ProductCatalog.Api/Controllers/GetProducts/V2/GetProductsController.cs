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
    /// <summary>Get a numbered page of products.</summary>
    /// <param name="request">Page defaults to 1; pageSize defaults to 10 and supports 1–100.</param>
    /// <param name="cancellationToken">Cancels the underlying catalog read.</param>
    /// <returns>Products in ascending SQL Server ID order and an indication of another page.</returns>
    /// <remarks>
    /// Pages beyond the catalog return an empty items array and hasNext false.
    /// Separate requests do not share a snapshot. Deep offset pages cost more to read.
    /// No total count is calculated. The calculated offset must not exceed 2147483647.
    /// </remarks>
    /// <response code="200">The requested page, possibly empty.</response>
    /// <response code="400">Invalid pagination parameters or unsupported offset.</response>
    /// <response code="500">An unexpected storage or server failure.</response>
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
