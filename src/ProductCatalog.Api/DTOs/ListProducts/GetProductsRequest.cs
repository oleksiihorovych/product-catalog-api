using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ProductCatalog.Application.Validation;

namespace ProductCatalog.Api.DTOs.ListProducts;

/// <summary>Parameters for selecting a product page.</summary>
public sealed record GetProductsRequest : IValidatableObject
{
    /// <summary>One-based page number; defaults to 1. The offset (page - 1) * pageSize must not exceed 2147483647.</summary>
    [Range(1, int.MaxValue)]
    [DefaultValue(1)]
    public int Page { get; init; } = 1;

    /// <summary>Number of products per page, from 1 to 100; defaults to 10.</summary>
    [Range(1, ProductPagination.MaxPageSize)]
    [DefaultValue(ProductPagination.DefaultPageSize)]
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
