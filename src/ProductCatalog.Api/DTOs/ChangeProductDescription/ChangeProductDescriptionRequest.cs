using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProductCatalog.Domain;

namespace ProductCatalog.Api.DTOs.ChangeProductDescription;

/// <summary>Requires description; null clears it. Unknown and duplicate properties are rejected.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeProductDescriptionRequest : IValidatableObject
{
    private string? description;
    private int descriptionAssignments;

    /// <summary>The exact new description; null clears it and whitespace is preserved.</summary>
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
