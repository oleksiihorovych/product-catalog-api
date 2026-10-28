using System.Runtime.CompilerServices;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Models;
using ProductCatalog.Application.Validation;
using ProductCatalog.Domain.Validation;

namespace ProductCatalog.Application.Services;

/// <summary>Coordinates product use cases through the selected repository.</summary>
public sealed class ProductApplicationService(
    IProductRepository repository, ILogger<ProductApplicationService> logger) : IProductApplicationService
{
    /// <inheritdoc/>
    public Task<ProductPage> ListPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        ProductPagination.GetOffset(page, pageSize);
        cancellationToken.ThrowIfCancellationRequested();
        var read = repository.ReadPageAsync(page, pageSize, cancellationToken);
        return logger.IsEnabled(LogLevel.Debug) ? LogPageResultAsync(read, page, pageSize) : read;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ProductDetails> ListAllAsync(CancellationToken cancellationToken)
    {
        var products = repository.ReadAllAsync(cancellationToken);
        // Avoid an extra iterator on the unbounded read unless its summary is enabled.
        return logger.IsEnabled(LogLevel.Debug) ? LogEnumeration(products, cancellationToken) : products;
    }

    /// <inheritdoc/>
    public Task<ProductDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The product ID must not be empty.", nameof(id));
        }

        var read = repository.ReadByIdAsync(id, cancellationToken);
        return logger.IsEnabled(LogLevel.Debug) ? LogProductResultAsync(read, id) : read;
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
            logger.LogDebug("Rejected description change for product {ProductId}: description exceeds the length limit", id);
            throw new ValidationException(validation.Errors);
        }

        return LogDescriptionResultAsync(repository.UpdateDescriptionAsync(id, description, cancellationToken),
            id, description is null);
    }

    private async Task<ProductPage> LogPageResultAsync(Task<ProductPage> read, int page, int pageSize)
    {
        var result = await read;
        logger.LogDebug("Read catalog page {Page} with page size {PageSize}: {ProductCount} products, has next: {HasNext}",
            page, pageSize, result.Items.Count, result.HasNext);
        return result;
    }

    private async Task<ProductDetails?> LogProductResultAsync(Task<ProductDetails?> read, Guid id)
    {
        var product = await read;
        logger.LogDebug("Read product {ProductId}; found: {Found}", id, product is not null);
        return product;
    }

    private async Task<bool> LogDescriptionResultAsync(Task<bool> save, Guid id, bool descriptionCleared)
    {
        var updated = await save;
        if (updated)
        {
            logger.LogInformation("Saved description for product {ProductId}; cleared: {DescriptionCleared}", id, descriptionCleared);
        }
        else
        {
            logger.LogWarning("Could not change description: product {ProductId} was not found", id);
        }

        return updated;
    }

    private async IAsyncEnumerable<ProductDetails> LogEnumeration(
        IAsyncEnumerable<ProductDetails> products,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long count = 0;
        var completed = false;
        try
        {
            await foreach (var product in products.WithCancellation(cancellationToken))
            {
                count++;
                yield return product;
            }

            completed = true;
        }
        finally
        {
            // Enumeration can fail or be disposed early; this does not claim HTTP delivery succeeded.
            logger.LogDebug("Catalog enumeration ended after {ProductCount} products; completed: {Completed}", count, completed);
        }
    }
}
