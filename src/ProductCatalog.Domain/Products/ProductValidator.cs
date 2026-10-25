using FluentValidation;

namespace ProductCatalog.Domain.Products;

internal sealed class ProductValidator : AbstractValidator<Product>
{
    internal static ProductValidator Instance { get; } = new();

    private ProductValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty()
            .WithErrorCode("Product.Id.Required");

        RuleFor(p => p.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("Product.Name.Required")
            .MaximumLength(DomainConstants.ProductMaxNameLength)
            .WithErrorCode("Product.Name.TooLong");

        RuleFor(p => p.ImgUri)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("Product.ImgUri.Required")
            .MaximumLength(DomainConstants.ProductMaxImageUriLength)
            .WithErrorCode("Product.ImgUri.TooLong")
            .Must(BeHttpUri)
            .WithMessage("Image URI must be an absolute HTTP or HTTPS URI.")
            .WithErrorCode("Product.ImgUri.Invalid");

        RuleFor(p => p.Price)
            .Cascade(CascadeMode.Stop)
            .InclusiveBetween(0m, DomainConstants.ProductMaxPrice)
            .WithErrorCode("Product.Price.OutOfRange")
            .Must(price => decimal.Round(price, 2) == price)
            .WithMessage("Price must have at most two decimal places.")
            .WithErrorCode("Product.Price.Precision");

        RuleFor(p => p.Description)
            .MaximumLength(DomainConstants.ProductMaxDescriptionLength)
            .WithErrorCode("Product.Description.TooLong");
    }

    private static bool BeHttpUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
