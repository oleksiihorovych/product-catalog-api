using FluentValidation;
using ProductCatalog.Domain.Validation;

namespace ProductCatalog.Domain.Models;

/// <summary>A product whose constructor enforces the catalog field constraints.</summary>
public sealed class Product
{
    /// <summary>Creates a product and validates its initial state.</summary>
    /// <param name="id">A nonempty GUID identifier.</param>
    /// <param name="name">A nonblank name within the configured domain length limit.</param>
    /// <param name="imgUri">An absolute HTTP or HTTPS image URI within the domain length limit.</param>
    /// <param name="price">A nonnegative price fitting decimal(18,2), without rounding.</param>
    /// <param name="description">Optional description; null represents no description.</param>
    /// <exception cref="ValidationException">
    /// One or more fields violate the domain constraints. Errors include field names and Product.* codes.
    /// </exception>
    public Product(Guid id, string name, string imgUri, decimal price, string? description)
    {
        Id = id;
        Name = name;
        ImgUri = imgUri;
        Price = price;
        Description = description;

        ProductValidator.Instance.ValidateAndThrow(this);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string ImgUri { get; private set; }

    public decimal Price { get; private set; }

    public string? Description { get; private set; }
}
