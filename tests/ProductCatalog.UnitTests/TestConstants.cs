namespace ProductCatalog.UnitTests;

internal static class TestConstants
{
    public const int EmptyCatalogCount = 0;
    public const int CatalogProductCount = 23;

    public static readonly Guid ProductId = CatalogProductId(1);

    public static Guid CatalogProductId(int number) =>
        Guid.Parse($"00000000-0000-0000-0000-{number:x12}");
    public const string ProductName = "Name";
    public const string UnicodeProductName = "Žluťoučký produkt";
    public const string ProductImageUri = "https://example.com/1.jpg";
    public const string CollectionImageUri = "https://example.com/image.jpg";
    public const decimal ProductPrice = 12.34m;
    public const string ProductDescription = "Description";

    public const string ProductsPath = "/api/v1/products";
    public const string TraceIdentifier = "unit-test-trace";
    public const string SensitiveErrorDetail = "sensitive-test-detail";
    public const string JsonContentType = "application/json";
    public const string ProblemJsonContentType = "application/problem+json";
    public const string PlainTextContentType = "text/plain";
    public const string Utf8PlainTextContentType = "text/plain; charset=utf-8";
    public const string RateLimitProblemJson = "{\"status\":429}";
    public const string RateLimitMessage = "Too many concurrent catalog listings.";
}
