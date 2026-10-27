using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.DTOs.ChangeProductDescription;
using ProductCatalog.Api.Validation;
using ProductCatalog.Application.Services;

namespace ProductCatalog.Api.Controllers.ChangeProductDescription;

[ApiController]
public sealed class ChangeProductDescriptionController(IProductApplicationService service) : ControllerBase
{
    /// <summary>Update a product's description.</summary>
    /// <param name="id">The product identifier, as a nonempty GUID.</param>
    /// <param name="request">The new description.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content after the description is saved.</returns>
    /// <remarks>
    /// Only the description is changed. Supply description exactly once; null clears it.
    /// Empty strings and whitespace are preserved. Additional fields are not accepted.
    /// </remarks>
    /// <response code="204">The description was saved. The response has no body.</response>
    /// <response code="400">The product ID or request body is invalid.</response>
    /// <response code="404">No product exists with the specified ID.</response>
    /// <response code="415">The request Content-Type is not application/json.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPatch("api/v1/products/{id}", Name = "ChangeProductDescriptionV1")]
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
