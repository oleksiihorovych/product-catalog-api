using FluentValidation.Results;
using ProductCatalog.Domain.Models;
using ProductCatalog.Domain;

namespace ProductCatalog.Domain.Validation;

/// <summary>Validates the description invariant for product creation and targeted updates.</summary>
public static class ProductDescriptionRules
{
    /// <summary>Accepts null, empty and whitespace values; limits length without normalizing the input.</summary>
    /// <returns>A result with Product.Description.TooLong when the domain limit is exceeded.</returns>
    public static ValidationResult Validate(string? description)
    {
        if (description?.Length > DomainConstants.ProductMaxDescriptionLength)
        {
            return new ValidationResult([
                new ValidationFailure(nameof(Product.Description),
                    $"Description must not exceed {DomainConstants.ProductMaxDescriptionLength} characters.")
                {
                    ErrorCode = "Product.Description.TooLong"
                }
            ]);
        }

        return new ValidationResult();
    }
}
