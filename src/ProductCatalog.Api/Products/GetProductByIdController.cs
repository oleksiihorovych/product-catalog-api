using ProductCatalog.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Api.Products;

[ApiController]
[Route("api/v1/products")]
public sealed class GetProductByIdController(IProductCatalogApplicationService catalog) : ControllerBase
{
    /// <summary>Get one product by ID.</summary>
    /// <param name="id">A nonempty GUID product identifier.</param>
    /// <param name="cancellationToken">Cancels the underlying product read.</param>
    /// <returns>The product, a validation problem for an invalid ID, or a not-found problem.</returns>
    [HttpGet("{id}")]
    [EndpointName("GetProductByIdV1")]
    [EndpointSummary("Get a product by ID")]
    [EndpointDescription("Returns one product. IDs must be nonempty GUIDs; invalid IDs return 400 and missing products return 404.")]
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
