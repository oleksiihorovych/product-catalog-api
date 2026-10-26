using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.Validation;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Api.Products;

[ApiController]
[Route("api/v1/products")]
public sealed class ChangeProductDescriptionController(IProductApplicationService service) : ControllerBase
{
    /// <summary>Changes only the product description.</summary>
    /// <param name="id">A nonempty GUID product identifier.</param>
    /// <param name="request">The required description; null clears it. Empty strings and whitespace are preserved.</param>
    /// <param name="cancellationToken">Cancels the operation and underlying persistence calls.</param>
    /// <returns>No content after a successful save, or a problem response.</returns>
    /// <remarks>
    /// Public operation. The last successful write determines the description; other product fields stay unchanged.
    /// Unknown and duplicate body fields are rejected; property names follow MVC case-insensitive matching.
    /// </remarks>
    /// <response code="204">The description was saved; the response has no body.</response>
    /// <response code="400">The body or ID is invalid.</response>
    /// <response code="404">The product does not exist.</response>
    /// <response code="415">The request media type is unsupported.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpPatch("{id}")]
    [EndpointName("ChangeProductDescriptionV1")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Consumes("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ChangeDescription(
        [FromRoute, NotEmptyGuid] Guid id,
        [FromBody, Required] ChangeProductDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await service.ChangeDescriptionAsync(id, request.Description, cancellationToken);
        return updated
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found.");
    }
}
