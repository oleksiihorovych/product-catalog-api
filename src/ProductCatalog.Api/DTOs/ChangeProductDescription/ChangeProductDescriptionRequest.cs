using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProductCatalog.Domain;

namespace ProductCatalog.Api.DTOs.ChangeProductDescription;

/// <summary>The new product description.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeProductDescriptionRequest : IValidatableObject
{
    private string? description;
    private int descriptionAssignments;

    /// <summary>Required property, at most 4000 characters; null clears the description. Empty strings and whitespace are preserved.</summary>
    [MaxLength(DomainConstants.ProductMaxDescriptionLength)]
    public required string? Description
    {
        get => description;
        init
        {
            description = value;
            descriptionAssignments++;
        }
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        // MVC validates after binding, so repeated JSON properties must not silently select the last value.
        if (descriptionAssignments != 1)
        {
            yield return new ValidationResult("Supply description exactly once.", [nameof(Description)]);
        }
    }
}
