using System.Globalization;
using System.Text.Json;
using ProductCatalog.Api.DTOs.Shared;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class ProductPriceSerializationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("123.45")]
    [InlineData("9007199254740991.99")]
    [InlineData("9999999999999999.99")]
    public void Response_PreservesDecimalPrecisionAsJsonNumber(string amount)
    {
        var price = decimal.Parse(amount, CultureInfo.InvariantCulture);
        var response = new ProductResponse(TestConstants.ProductId, "Product", "https://example.com/image.jpg", price, null);

        var json = JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement.GetProperty("price");

        Assert.Equal(JsonValueKind.Number, value.ValueKind);
        Assert.Equal(price, value.GetDecimal());
        Assert.Equal(price, JsonSerializer.Deserialize<ProductResponse>(json, JsonSerializerOptions.Web)!.Price);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("description").ValueKind);
    }
}
