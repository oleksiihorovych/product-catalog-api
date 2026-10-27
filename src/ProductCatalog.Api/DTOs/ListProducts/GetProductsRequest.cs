using System.ComponentModel.DataAnnotations;
using ProductCatalog.Application.Validation;

namespace ProductCatalog.Api.DTOs.ListProducts;

/// <summary>Numbered catalog pagination parameters.</summary>
public sealed record GetProductsRequest : IValidatableObject
{
    /// <summary>One-based page number; defaults to 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>Items per page, from 1 to 100; defaults to 10.</summary>
    [Range(1, ProductPagination.MaxPageSize)]
    public int PageSize { get; init; } = ProductPagination.DefaultPageSize;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Page >= 1 && PageSize is >= 1 and <= ProductPagination.MaxPageSize &&
            ((long)Page - 1) * PageSize > int.MaxValue)
        {
            yield return new ValidationResult("The requested page offset exceeds the supported range.", [nameof(Page)]);
        }
    }
}
