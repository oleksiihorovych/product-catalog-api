using System.Runtime.CompilerServices;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Models;
using ProductCatalog.Application.Services;
using ProductCatalog.Domain;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class ProductProcessLoggingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Private description that must not appear in logs")]
    public async Task DescriptionChange_LogsSuccessOnlyAfterSaveWithoutDescriptionContent(string? description)
    {
        var saved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs);
        var service = new ProductApplicationService(new StubRepository { Save = _ => saved.Task },
            factory.CreateLogger<ProductApplicationService>());

        var operation = service.ChangeDescriptionAsync(TestConstants.ProductId, description, CancellationToken.None);

        Assert.False(operation.IsCompleted);
        Assert.Empty(logs.Entries);

        saved.SetResult(true);
        Assert.True(await operation);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(TestConstants.ProductId, entry.Properties["ProductId"]);
        Assert.Equal(description is null, entry.Properties["DescriptionCleared"]);
        Assert.DoesNotContain("Description", entry.Properties.Keys);
        if (description is not null) Assert.DoesNotContain(description, entry.Message);

    }

    [Theory]
    [InlineData("page", "pending")]
    [InlineData("page", "faulted")]
    [InlineData("page", "canceled")]
    [InlineData("lookup", "pending")]
    [InlineData("lookup", "faulted")]
    [InlineData("lookup", "canceled")]
    [InlineData("description", "pending")]
    [InlineData("description", "faulted")]
    [InlineData("description", "canceled")]
    public async Task Operations_LogResultsOnlyOnCompletionAndPropagateFailureOrCancellation(string operation, string state)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = new InvalidOperationException("Storage failed");
        Task<T> Result<T>(TaskCompletionSource<T> pending) => state switch
        {
            "faulted" => Task.FromException<T>(failure),
            "canceled" => Task.FromCanceled<T>(cancellation.Token),
            _ => pending.Task
        };
        var page = new TaskCompletionSource<ProductPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var product = new TaskCompletionSource<ProductDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new StubRepository
        {
            Page = (_, _, _) => Result(page),
            Lookup = (_, _) => Result(product),
            Save = _ => Result(saved)
        };
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs);
        var service = new ProductApplicationService(repository, factory.CreateLogger<ProductApplicationService>());
        var actual = Invoke(service, operation, CancellationToken.None);

        Assert.Equal(1, repository.Calls);
        Assert.Empty(logs.Entries);
        if (state == "pending")
        {
            Assert.False(actual.IsCompleted);
            page.SetResult(new ProductPage([Product(1)], 1, 10, false));
            product.SetResult(Product(1));
            saved.SetResult(true);
            await actual;
            var entry = Assert.Single(logs.Entries);
            Assert.Equal(operation == "description" ? LogLevel.Information : LogLevel.Debug, entry.Level);
            Assert.DoesNotContain("Private", entry.Message);
        }
        else if (state == "faulted")
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => actual));
            Assert.Empty(logs.Entries);
        }
        else
        {
            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => actual);
            Assert.Equal(cancellation.Token, canceled.CancellationToken);
            Assert.True(actual.IsCanceled);
            Assert.Empty(logs.Entries);
        }
    }

    [Theory]
    [InlineData("page")]
    [InlineData("lookup")]
    [InlineData("description")]
    public void Operations_PreserveSynchronousRepositoryExceptions(string operation)
    {
        var failure = new InvalidOperationException("Storage failed before returning a task");
        var repository = new StubRepository
        {
            Page = (_, _, _) => throw failure,
            Lookup = (_, _) => throw failure,
            Save = _ => throw failure
        };
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs);
        var service = new ProductApplicationService(repository, factory.CreateLogger<ProductApplicationService>());

        var actual = Record.Exception(() => { _ = Invoke(service, operation, CancellationToken.None); });

        Assert.Same(failure, actual);
        Assert.Equal(1, repository.Calls);
        Assert.Empty(logs.Entries);
    }

    [Theory]
    [InlineData("page", typeof(ArgumentOutOfRangeException))]
    [InlineData("lookup", typeof(ArgumentException))]
    [InlineData("description-id", typeof(ArgumentException))]
    [InlineData("description-length", typeof(ValidationException))]
    [InlineData("canceled-page", typeof(OperationCanceledException))]
    [InlineData("canceled-description", typeof(OperationCanceledException))]
    public void Validation_StillThrowsSynchronouslyBeforeRepositoryCall(string operation, Type exceptionType)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new StubRepository();
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs);
        var service = new ProductApplicationService(repository, factory.CreateLogger<ProductApplicationService>());

        var actual = Record.Exception(() =>
        {
            _ = operation switch
            {
                "page" => service.ListPageAsync(0, 10, CancellationToken.None),
                "lookup" => service.GetByIdAsync(Guid.Empty, CancellationToken.None),
                "description-id" => service.ChangeDescriptionAsync(Guid.Empty, null, CancellationToken.None),
                "description-length" => service.ChangeDescriptionAsync(TestConstants.ProductId,
                    new string('x', DomainConstants.ProductMaxDescriptionLength + 1), CancellationToken.None),
                "canceled-page" => service.ListPageAsync(1, 10, cancellation.Token),
                _ => (Task)service.ChangeDescriptionAsync(TestConstants.ProductId, null, cancellation.Token)
            };
        });

        Assert.IsType(exceptionType, actual);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task DescriptionChange_MissingProductProducesOneWarningWithoutSuccess()
    {
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs);
        var service = new ProductApplicationService(new StubRepository { Save = _ => Task.FromResult(false) },
            factory.CreateLogger<ProductApplicationService>());

        Assert.False(await service.ChangeDescriptionAsync(TestConstants.ProductId, "private", CancellationToken.None));

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(TestConstants.ProductId, entry.Properties["ProductId"]);
        Assert.DoesNotContain("private", entry.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public async Task FullListing_RemainsLazyAndLogsOneSummaryAfterCompleteEnumeration(int count)
    {
        var enumerated = 0;
        async IAsyncEnumerable<ProductDetails> Products()
        {
            await Task.CompletedTask;
            for (var i = 1; i <= count; i++)
            {
                enumerated++;
                yield return Product(i);
            }
        }

        using var cancellation = new CancellationTokenSource();
        var products = Products();
        var repository = new StubRepository { Products = products };
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs);
        var service = new ProductApplicationService(repository, factory.CreateLogger<ProductApplicationService>());

        var stream = service.ListAllAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, repository.ListToken);
        Assert.Equal(0, enumerated);
        Assert.Empty(logs.Entries);

        await foreach (var _ in stream) { }

        Assert.Equal(count, enumerated);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal((long)count, entry.Properties["ProductCount"]);
        Assert.Equal(true, entry.Properties["Completed"]);
        Assert.DoesNotContain("Private", entry.Message);
    }

    [Theory]
    [InlineData("dispose")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task FullListing_InterruptedStreamDoesNotReportCompletionOrReadAhead(string interruption)
    {
        var failure = new InvalidOperationException("Deferred storage failure");
        var enumerated = 0;
        async IAsyncEnumerable<ProductDetails> Products([EnumeratorCancellation] CancellationToken token = default)
        {
            await Task.CompletedTask;
            enumerated++;
            yield return Product(1);
            token.ThrowIfCancellationRequested();
            if (interruption == "failure") throw failure;
            enumerated++;
            yield return Product(2);
        }

        using var cancellation = new CancellationTokenSource();
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs);
        var service = new ProductApplicationService(new StubRepository { Products = Products() },
            factory.CreateLogger<ProductApplicationService>());
        await using (var stream = service.ListAllAsync(CancellationToken.None).GetAsyncEnumerator(cancellation.Token))
        {
            Assert.True(await stream.MoveNextAsync());
            Assert.Equal(1, enumerated);
            Assert.Empty(logs.Entries);
            if (interruption == "failure")
            {
                Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () => await stream.MoveNextAsync()));
            }
            else if (interruption == "cancel")
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());
            }
        }

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal(1L, entry.Properties["ProductCount"]);
        Assert.Equal(false, entry.Properties["Completed"]);
        Assert.Equal(1, enumerated);
    }

    [Fact]
    public void Reads_ReturnOriginalTasksAndStreamWhenDebugIsDisabled()
    {
        var page = Task.FromResult(new ProductPage([], 1, 10, false));
        var product = Task.FromResult<ProductDetails?>(Product(1));
        var repository = new StubRepository { Page = (_, _, _) => page, Lookup = (_, _) => product };
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs, LogLevel.Information);
        var service = new ProductApplicationService(repository, factory.CreateLogger<ProductApplicationService>());

        Assert.Same(page, service.ListPageAsync(1, 10, CancellationToken.None));
        Assert.Same(product, service.GetByIdAsync(TestConstants.ProductId, CancellationToken.None));
        Assert.Same(repository.Products, service.ListAllAsync(CancellationToken.None));
        Assert.Empty(logs.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lookup_LogsFoundStatusWithoutProductContent(bool found)
    {
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs);
        var service = new ProductApplicationService(new StubRepository
        {
            Lookup = (_, _) => Task.FromResult<ProductDetails?>(found ? Product(1) : null)
        }, factory.CreateLogger<ProductApplicationService>());

        await service.GetByIdAsync(TestConstants.ProductId, CancellationToken.None);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal(found, entry.Properties["Found"]);
        Assert.Equal(TestConstants.ProductId, entry.Properties["ProductId"]);
        Assert.DoesNotContain("Private", entry.Message);
    }

    [Fact]
    public async Task FrequentReads_AreSilentAtInformationLevel()
    {
        var logs = new RecordingLogProvider();
        using var factory = CreateFactory(logs, LogLevel.Information);
        var service = new ProductApplicationService(new StubRepository(), factory.CreateLogger<ProductApplicationService>());

        Assert.Null(await service.GetByIdAsync(TestConstants.ProductId, CancellationToken.None));
        Assert.Empty((await service.ListPageAsync(1, 10, CancellationToken.None)).Items);
        await foreach (var _ in service.ListAllAsync(CancellationToken.None)) { }

        Assert.Empty(logs.Entries);
    }

    private static Task Invoke(ProductApplicationService service, string operation, CancellationToken token) =>
        operation switch
        {
            "page" => service.ListPageAsync(1, 10, token),
            "lookup" => service.GetByIdAsync(TestConstants.ProductId, token),
            _ => service.ChangeDescriptionAsync(TestConstants.ProductId, "private", token)
        };

    private static ILoggerFactory CreateFactory(RecordingLogProvider logs, LogLevel level = LogLevel.Debug) =>
        LoggerFactory.Create(logging => logging.SetMinimumLevel(level).AddProvider(logs));

    private static ProductDetails Product(int id) => new(TestConstants.CatalogProductId(id), "Private product name",
        TestConstants.CollectionImageUri, TestConstants.ProductPrice, "Private description");

    private sealed class StubRepository : IProductRepository
    {
        public Func<CancellationToken, Task<bool>> Save { get; init; } = _ => throw new InvalidOperationException("Unexpected write");
        public Func<Guid, CancellationToken, Task<ProductDetails?>> Lookup { get; init; } = (_, _) => Task.FromResult<ProductDetails?>(null);
        public Func<int, int, CancellationToken, Task<ProductPage>> Page { get; init; } =
            (page, size, _) => Task.FromResult(new ProductPage([], page, size, false));
        public IAsyncEnumerable<ProductDetails> Products { get; init; } = Empty();
        public int Calls { get; private set; }
        public CancellationToken ListToken { get; private set; }

        public Task<bool> UpdateDescriptionAsync(Guid id, string? description, CancellationToken cancellationToken)
        {
            Calls++;
            return Save(cancellationToken);
        }

        public IAsyncEnumerable<ProductDetails> ReadAllAsync(CancellationToken cancellationToken)
        {
            Calls++;
            ListToken = cancellationToken;
            return Products;
        }

        public Task<ProductDetails?> ReadByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Lookup(id, cancellationToken);
        }

        public Task<ProductPage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            Calls++;
            return Page(page, pageSize, cancellationToken);
        }

        private static async IAsyncEnumerable<ProductDetails> Empty()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
