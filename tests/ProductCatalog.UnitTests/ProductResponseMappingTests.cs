using System.Runtime.CompilerServices;
using Xunit;
using ProductCatalog.Api.DTOs.Shared;
using ProductCatalog.Api.Mappings;
using ProductCatalog.Application.Models;

namespace ProductCatalog.UnitTests;

public sealed class ProductResponseMappingTests
{
    [Fact]
    public async Task Mapping_IsLazyAndPreservesEveryField()
    {
        var source = new TrackingSequence();
        var responses = source.Read().ToResponses();

        Assert.False(source.Started);

        await using var enumerator = responses.GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(new ProductResponse(TestConstants.ProductId, TestConstants.UnicodeProductName, TestConstants.ProductImageUri, TestConstants.ProductPrice, null), enumerator.Current);
        Assert.False(source.Disposed);
        Assert.False(await enumerator.MoveNextAsync());
        Assert.True(source.Disposed);
    }

    [Fact]
    public async Task Mapping_DisposesSourceWhenConsumerStopsEarly()
    {
        var source = new TrackingSequence();

        await using (var enumerator = source.Read().ToResponses().GetAsyncEnumerator())
        {
            Assert.True(await enumerator.MoveNextAsync());
        }

        Assert.True(source.Disposed);
    }

    [Fact]
    public async Task Mapping_PropagatesEnumeratorCancellationAndDisposesSource()
    {
        var source = new TrackingSequence();
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = source.Read().ToResponses().GetAsyncEnumerator(cancellation.Token);

        Assert.True(await enumerator.MoveNextAsync());
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());

        Assert.Equal(cancellation.Token, source.ReceivedToken);
        Assert.True(source.Disposed);
    }

    [Fact]
    public async Task Mapping_PropagatesMidStreamFailureAndDisposesSource()
    {
        var expected = new InvalidOperationException("source failure");
        var source = new TrackingSequence(expected);
        await using var enumerator = source.Read().ToResponses().GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () => await enumerator.MoveNextAsync());

        Assert.Same(expected, actual);
        Assert.True(source.Disposed);
    }

    private sealed class TrackingSequence(Exception? failure = null)
    {
        public bool Started { get; private set; }

        public bool Disposed { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public async IAsyncEnumerable<ProductDetails> Read(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Started = true;
            ReceivedToken = cancellationToken;

            try
            {
                await Task.CompletedTask;
                cancellationToken.ThrowIfCancellationRequested();

                yield return new ProductDetails(TestConstants.ProductId, TestConstants.UnicodeProductName, TestConstants.ProductImageUri, TestConstants.ProductPrice, null);

                cancellationToken.ThrowIfCancellationRequested();

                if (failure is not null)
                {
                    throw failure;
                }
            }
            finally
            {
                Disposed = true;
            }
        }
    }
}
