using ProductCatalog.Api.DTOs.Shared;

namespace ProductCatalog.Api.DTOs.ListProducts;

/// <summary>A numbered product page; HasNext indicates another item existed at read time.</summary>
public sealed record ProductListResponse(IReadOnlyList<ProductResponse> Items, int Page, int PageSize, bool HasNext);
