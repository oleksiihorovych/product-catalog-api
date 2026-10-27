using ProductCatalog.Api.DTOs.Shared;

namespace ProductCatalog.Api.DTOs.ListProducts;

/// <summary>A product page with pagination details.</summary>
/// <param name="Items">Products on this page; empty when the page is beyond the catalog.</param>
/// <param name="Page">The requested one-based page number.</param>
/// <param name="PageSize">The requested maximum number of products per page.</param>
/// <param name="HasNext">Whether more products followed this page when it was read.</param>
public sealed record ProductListResponse(IReadOnlyList<ProductResponse> Items, int Page, int PageSize, bool HasNext);
