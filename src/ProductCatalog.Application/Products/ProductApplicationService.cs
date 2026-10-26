using FluentValidation;
using ProductCatalog.Domain.Products;

namespace ProductCatalog.Application.Products;

/// <summary>Coordinates product use cases through the selected repository.</summary>
public sealed class ProductApplicationService(IProductRepository repository) : IProductApplicationService
{
    /// <inheritdoc/>
    public Task<ProductPage> ListPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        ProductPagination.GetOffset(page, pageSize);
        cancellationToken.ThrowIfCancellationRequested();
        return repository.ReadPageAsync(page, pageSize, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken) =>
        repository.ReadAllAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<ProductDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The product ID must not be empty.", nameof(id));
        }

        return repository.ReadByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> ChangeDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The product ID must not be empty.", nameof(id));
        }

        var validation = ProductDescriptionRules.Validate(description);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        return repository.UpdateDescriptionAsync(id, description, cancellationToken);
    }
}
