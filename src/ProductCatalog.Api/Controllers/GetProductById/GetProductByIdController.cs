using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.DTOs.Shared;
using ProductCatalog.Api.Mappings;
using ProductCatalog.Api.Validation;
using ProductCatalog.Application.Services;

namespace ProductCatalog.Api.Controllers.GetProductById;

[ApiController]
public sealed class GetProductByIdController(IProductApplicationService catalog) : ControllerBase
{
    /// <summary>Get a product by ID.</summary>
    /// <param name="id">The product identifier, as a nonempty GUID.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The product matching the specified ID.</returns>
    /// <response code="200">The requested product.</response>
    /// <response code="400">The product ID is not a valid nonempty GUID.</response>
    /// <response code="404">No product exists with the specified ID.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("api/v1/products/{id}", Name = "GetProductByIdV1")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ProductResponse>> GetById(
        [FromRoute, NotEmptyGuid] Guid id,
        CancellationToken cancellationToken)
    {
        var product = await catalog.GetByIdAsync(id, cancellationToken);

        if (product is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found.");
        }

        return Ok(product.ToResponse());
    }
}
