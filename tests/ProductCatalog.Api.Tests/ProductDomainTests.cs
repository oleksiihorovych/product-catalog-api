using ProductCatalog.Domain.Products;
using Xunit;

namespace ProductCatalog.Api.Tests;

public sealed class ProductDomainTests
{
    [Fact]
    public void Product_AcceptsNullDescriptionAndExactDecimalPrice()
    {
        var product = new Product(1, "Name", "https://example.com/1.jpg", 12.34m, null);
        Assert.Equal(12.34m, product.Price);
        Assert.Null(product.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Product_RejectsNonpositiveId(long id) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(id, "Name", "https://example.com/1.jpg", 1, null));

    [Theory]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public void Product_RejectsPriceOutsideSqlPrecision(string price) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(1, "Name", "https://example.com/1.jpg",
            decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), null));

    [Theory]
    [InlineData("/image.jpg")]
    [InlineData("file:///image.jpg")]
    [InlineData("invalid")]
    public void Product_RejectsInvalidImageUri(string imgUri) =>
        Assert.Throws<ArgumentException>(() => new Product(1, "Name", imgUri, 1, null));

    [Fact]
    public void Product_RejectsEmptyOrOversizedText()
    {
        Assert.Throws<ArgumentException>(() => new Product(1, " ", "https://example.com/1.jpg", 0, null));
        Assert.Throws<ArgumentException>(() => new Product(1, new string('x', 201), "https://example.com/1.jpg", 0, null));
        Assert.Throws<ArgumentException>(() => new Product(1, "Name", "https://example.com/1.jpg", 0, new string('x', 4001)));
    }
}
