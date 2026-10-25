namespace ProductCatalog.Domain.Products;

public sealed class Product
{
    public const int MaxNameLength = 200;
    public const int MaxImageUriLength = 2048;
    public const int MaxDescriptionLength = 4000;
    public const decimal MaxPrice = 9999999999999999.99m;

    public Product(long id, string name, string imgUri, decimal price, string? description)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(imgUri);
        if (name.Length > MaxNameLength)
            throw new ArgumentException("Product name is too long.", nameof(name));
        if (imgUri.Length > MaxImageUriLength ||
            !Uri.TryCreate(imgUri, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Image URI must be an absolute HTTP or HTTPS URI.", nameof(imgUri));
        if (price < 0 || price > MaxPrice || decimal.Round(price, 2) != price)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must fit decimal(18,2) and be nonnegative.");
        if (description?.Length > MaxDescriptionLength)
            throw new ArgumentException("Description is too long.", nameof(description));

        Id = id;
        Name = name;
        ImgUri = imgUri;
        Price = price;
        Description = description;
    }

    public long Id { get; private set; }
    public string Name { get; private set; }
    public string ImgUri { get; private set; }
    public decimal Price { get; private set; }
    public string? Description { get; private set; }
}
