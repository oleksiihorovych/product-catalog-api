using FluentValidation;
using Xunit;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Models;
using ProductCatalog.Application.Services;
using ProductCatalog.Domain;

namespace ProductCatalog.UnitTests;

public sealed class ProductApplicationServiceDescriptionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Updated description")]
    public async Task ChangeDescription_PerformsOneTargetedSaveAndForwardsArguments(string? description)
    {
        var repository = new FakeRepository();
        var service = new ProductApplicationService(repository);
        using var cancellation = new CancellationTokenSource();

        Assert.True(await service.ChangeDescriptionAsync(TestConstants.ProductId, description, cancellation.Token));
        Assert.Equal(1, repository.Calls);
        Assert.Equal(TestConstants.ProductId, repository.Id);
        Assert.Equal(description, repository.Description);
        Assert.Equal(cancellation.Token, repository.Token);
    }

    [Fact]
    public async Task ChangeDescription_ReturnsFalseWhenNoProductMatched()
    {
        var repository = new FakeRepository { Save = _ => Task.FromResult(false) };

        Assert.False(await new ProductApplicationService(repository)
            .ChangeDescriptionAsync(TestConstants.ProductId, null, CancellationToken.None));
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task ChangeDescription_RejectsEmptyIdBeforeSaving()
    {
        var repository = new FakeRepository();
        var service = new ProductApplicationService(repository);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ChangeDescriptionAsync(Guid.Empty, null, CancellationToken.None));

        Assert.Equal("id", exception.ParamName);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ChangeDescription_RejectsInvalidDescriptionBeforeSaving()
    {
        var repository = new FakeRepository();
        var service = new ProductApplicationService(repository);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ChangeDescriptionAsync(TestConstants.ProductId,
                new string('x', DomainConstants.ProductMaxDescriptionLength + 1), CancellationToken.None));

        Assert.Equal("Product.Description.TooLong", Assert.Single(exception.Errors).ErrorCode);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ChangeDescription_AcceptsDomainLengthBoundary()
    {
        var repository = new FakeRepository();
        var description = new string('x', DomainConstants.ProductMaxDescriptionLength);

        Assert.True(await new ProductApplicationService(repository)
            .ChangeDescriptionAsync(TestConstants.ProductId, description, CancellationToken.None));
        Assert.Equal(description, repository.Description);
    }

    [Fact]
    public async Task ChangeDescription_ReportsSuccessOnlyWhenSaveCompletes()
    {
        var saved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new FakeRepository { Save = _ => saved.Task };
        var operation = new ProductApplicationService(repository)
            .ChangeDescriptionAsync(TestConstants.ProductId, null, CancellationToken.None);

        Assert.False(operation.IsCompleted);
        saved.SetResult(true);
        Assert.True(await operation);
    }

    [Fact]
    public async Task ChangeDescription_PropagatesSaveFailure()
    {
        var failure = new InvalidOperationException("Save failed.");
        var repository = new FakeRepository { Save = _ => Task.FromException<bool>(failure) };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProductApplicationService(repository)
                .ChangeDescriptionAsync(TestConstants.ProductId, null, CancellationToken.None));

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task ChangeDescription_HonorsCancellationBeforeSaving()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new FakeRepository();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ProductApplicationService(repository)
                .ChangeDescriptionAsync(TestConstants.ProductId, null, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, repository.Calls);
    }

    private sealed class FakeRepository : IProductRepository
    {
        public Task<ProductPage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected paginated read.");

        public Func<CancellationToken, Task<bool>> Save { get; init; } = _ => Task.FromResult(true);
        public int Calls { get; private set; }
        public Guid Id { get; private set; }
        public string? Description { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<bool> UpdateDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken)
        {
            Calls++;
            Id = id;
            Description = description;
            Token = cancellationToken;
            return Save(cancellationToken);
        }

        public Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A targeted update must not read the product first.");

        public IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A targeted update must not read the catalog.");
    }
}
