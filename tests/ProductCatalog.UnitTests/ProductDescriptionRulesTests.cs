using FluentValidation;
using Xunit;
using ProductCatalog.Domain.Models;
using ProductCatalog.Domain.Validation;

namespace ProductCatalog.UnitTests;

public sealed class ProductDescriptionRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("New description")]
    public void DescriptionRulesAndConstruction_AcceptExactValue(string? description)
    {
        Assert.True(ProductDescriptionRules.Validate(description).IsValid);
        Assert.Equal(description, CreateProduct(description).Description);
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void DescriptionRulesAndConstruction_EnforceSameLengthLimit(int length, bool valid)
    {
        var description = new string('x', length);
        var result = ProductDescriptionRules.Validate(description);

        Assert.Equal(valid, result.IsValid);
        if (valid)
        {
            Assert.Equal(description, CreateProduct(description).Description);
        }
        else
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal(nameof(Product.Description), error.PropertyName);
            Assert.Equal("Product.Description.TooLong", error.ErrorCode);
            var exception = Assert.Throws<ValidationException>(() => CreateProduct(description));
            Assert.Equal(error.ErrorCode, Assert.Single(exception.Errors).ErrorCode);
        }
    }

    private static Product CreateProduct(string? description) =>
        new(TestConstants.ProductId, TestConstants.ProductName, TestConstants.ProductImageUri,
            TestConstants.ProductPrice, description);
}
