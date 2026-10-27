using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.DTOs.Shared;
using ProductCatalog.Api.Mappings;
using ProductCatalog.Api.Validation;
using ProductCatalog.Application.Services;

namespace ProductCatalog.Api.Controllers.GetProductById;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/v1/products")]
public sealed class GetProductByIdController(IProductApplicationService catalog) : ControllerBase
{
    /// <summary>Get one product by ID.</summary>
    /// <param name="id">A nonempty GUID product identifier.</param>
    /// <param name="cancellationToken">Cancels the underlying product read.</param>
    /// <returns>The product, a validation problem for an invalid ID, or a not-found problem.</returns>
    /// <remarks>IDs must be nonempty GUIDs; the card contains the same fields as a catalog entry.</remarks>
    /// <response code="200">The product card.</response>
    /// <response code="400">The ID is malformed or empty.</response>
    /// <response code="404">The product does not exist.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpGet("{id}")]
    [EndpointName("GetProductByIdV1")]
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
