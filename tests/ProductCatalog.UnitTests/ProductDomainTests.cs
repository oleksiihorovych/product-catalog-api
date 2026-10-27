using FluentValidation;
using Xunit;
using ProductCatalog.Domain.Models;
using ProductCatalog.Domain;

namespace ProductCatalog.UnitTests;

public sealed class ProductDomainTests
{
    [Fact]
    public void Product_AcceptsNullDescriptionAndExactDecimalPrice()
    {
        var product = new Product(TestConstants.ProductId, TestConstants.ProductName, TestConstants.ProductImageUri, TestConstants.ProductPrice, null);

        Assert.Equal(TestConstants.ProductPrice, product.Price);
        Assert.Null(product.Description);
    }

    [Fact]
    public void Product_CollectsErrorsAcrossFieldsWithStableCodes()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            new Product(Guid.Empty, " ", "file:///image.jpg", -1m, null));

        Assert.Collection(exception.Errors,
            error => Assert.Equal(("Id", "Product.Id.Required"), (error.PropertyName, error.ErrorCode)),
            error => Assert.Equal(("Name", "Product.Name.Required"), (error.PropertyName, error.ErrorCode)),
            error => Assert.Equal(("ImgUri", "Product.ImgUri.Invalid"), (error.PropertyName, error.ErrorCode)),
            error => Assert.Equal(("Price", "Product.Price.OutOfRange"), (error.PropertyName, error.ErrorCode)));
    }

    [Fact]
    public void Product_AcceptsBoundaryLengthsAndPrice()
    {
        var product = new Product(TestConstants.ProductId, new string('x', DomainConstants.ProductMaxNameLength),
            TestConstants.ProductImageUri, DomainConstants.ProductMaxPrice,
            new string('x', DomainConstants.ProductMaxDescriptionLength));

        Assert.Equal(DomainConstants.ProductMaxPrice, product.Price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Product_RejectsMissingRequiredText(string? value)
    {
        Assert.Throws<ValidationException>(() => new Product(TestConstants.ProductId, value!, TestConstants.ProductImageUri, 0, null));
        Assert.Throws<ValidationException>(() => new Product(TestConstants.ProductId, TestConstants.ProductName, value!, 0, null));
    }

    [Fact]
    public void Product_RejectsEmptyId() =>
        Assert.Throws<ValidationException>(() => new Product(Guid.Empty, TestConstants.ProductName, TestConstants.ProductImageUri, 1, null));

    [Theory]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public void Product_RejectsPriceOutsideSqlPrecision(string price) =>
        Assert.Throws<ValidationException>(() => new Product(TestConstants.ProductId, TestConstants.ProductName, TestConstants.ProductImageUri,
            decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), null));

    [Theory]
    [InlineData("/image.jpg")]
    [InlineData("file:///image.jpg")]
    [InlineData("invalid")]
    public void Product_RejectsInvalidImageUri(string imgUri) =>
        Assert.Throws<ValidationException>(() => new Product(TestConstants.ProductId, TestConstants.ProductName, imgUri, 1, null));

    [Fact]
    public void Product_RejectsEmptyOrOversizedText()
    {
        Assert.Throws<ValidationException>(() => new Product(TestConstants.ProductId, " ", TestConstants.ProductImageUri, 0, null));
        Assert.Throws<ValidationException>(() => new Product(TestConstants.ProductId, new string('x', 201), TestConstants.ProductImageUri, 0, null));
        Assert.Throws<ValidationException>(() => new Product(TestConstants.ProductId, TestConstants.ProductName, TestConstants.ProductImageUri, 0, new string('x', 4001)));
    }
}
