namespace ProductCatalog.Application.Products;

/// <summary>Defines the supported numbered-page range and offset calculation.</summary>
public static class ProductPagination
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    /// <summary>Validates page parameters and returns the offset supported by EF Skip.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A parameter or the calculated offset is unsupported.</exception>
    public static int GetOffset(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        var offset = ((long)page - 1) * pageSize;
        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "The requested page offset exceeds the supported range.");
        }

        return (int)offset;
    }
}
