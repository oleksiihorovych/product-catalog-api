namespace ProductCatalog.Application.Models;

/// <summary>A bounded page in SQL Server ID order, without a total-count query.</summary>
/// <remarks>Separate page reads do not share a snapshot. HasNext describes this read only.</remarks>
public sealed record ProductPage(IReadOnlyList<ProductDetails> Items, int Page, int PageSize, bool HasNext);
