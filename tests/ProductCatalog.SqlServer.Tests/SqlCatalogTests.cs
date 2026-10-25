using System.Data;
using System.Data.Common;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using ProductCatalog.Api.Products;
using ProductCatalog.Domain.Products;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Infrastructure.Products;
using Xunit;

namespace ProductCatalog.SqlServer.Tests;

// Each test owns a new randomly named database. No test reuses or clears the supplied catalog.
public sealed class SqlCatalogTests : IAsyncLifetime
{
    private readonly string connection;

    public SqlCatalogTests()
    {
        var configured = Environment.GetEnvironmentVariable("CATALOG_TEST_SQL")
            ?? throw new InvalidOperationException("Set CATALOG_TEST_SQL to a dedicated SQL Server test instance. No SQL checks were run.");
        var builder = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = "CatalogTests_" + Guid.NewGuid().ToString("N"),
            Pooling = false,
            ConnectRetryCount = 0
        };
        connection = builder.ConnectionString;
    }

    private CatalogDbContext CreateContext(DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(connection);
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return new(options.Options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await CatalogDatabaseSetup.InitializeAsync(db, seed: false, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task EmptyMigratedDatabase_ReturnsEmptyArrayAndUsesReadCommittedSnapshot()
    {
        await using var db = CreateContext();
        Assert.True(await db.Database.SqlQueryRaw<bool>(
            "SELECT is_read_committed_snapshot_on AS [Value] FROM sys.databases WHERE database_id = DB_ID()")
            .SingleAsync());
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RepeatableSeed_HttpAndSqlReturnExactlyTheDemoCatalogInOneUntrackedRead()
    {
        await using (var setup = CreateContext())
        {
            await CatalogDatabaseSetup.InitializeAsync(setup, seed: true, CancellationToken.None);
            await CatalogDatabaseSetup.InitializeAsync(setup, seed: true, CancellationToken.None);
        }
        var interceptor = new ReadCounter();
        await using var db = CreateContext(interceptor);
        var actual = new List<ProductResponse>();
        await foreach (var p in new SqlProductReader(db).StreamAllAsync(CancellationToken.None))
            actual.Add(new(p.Id, p.Name, p.ImgUri, p.Price, p.Description));
        Assert.Equal(1, interceptor.Reads);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.False(db.Database.CreateExecutionStrategy().RetriesOnFailure);
        var expected = DemoProducts.Create().Select(p => new ProductResponse(p.Id, p.Name, p.ImgUri, p.Price, p.Description));
        Assert.Equal(expected, actual);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var http = await client.GetFromJsonAsync<ProductResponse[]>("/api/v1/products");
        Assert.Equal(actual, http!);
    }

    [Fact]
    public async Task DecimalAndUnicode_ArePreservedAndCommittedChangesAreReadDirectly()
    {
        await using var db = CreateContext();
        db.Products.Add(new Product(101, "Žluťoučký produkt", "https://example.com/101.jpg", 12.34m, null));
        await db.SaveChangesAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<ProductResponse[]>("/api/v1/products");
        Assert.Equal(12.34m, Assert.Single(before!).Price);
        Assert.Equal("Žluťoučký produkt", before![0].Name);
        await db.Products.Where(p => p.Id == 101).ExecuteUpdateAsync(s => s.SetProperty(p => p.Description, "Committed"));
        var after = await client.GetFromJsonAsync<ProductResponse[]>("/api/v1/products");
        Assert.Equal("Committed", Assert.Single(after!).Description);
    }

    [Fact]
    public async Task CancelledEnumeration_ClosesReaderAndConnection()
    {
        await using var db = CreateContext();
        await CatalogDatabaseSetup.InitializeAsync(db, seed: true, CancellationToken.None);
        db.ChangeTracker.Clear();
        using var cancellation = new CancellationTokenSource();
        var enumerator = new SqlProductReader(db).StreamAllAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        try
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.Equal(ConnectionState.Open, db.Database.GetDbConnection().State);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task SnapshotListing_DoesNotWaitForUncommittedWriter()
    {
        await using var writer = CreateContext();
        await CatalogDatabaseSetup.InitializeAsync(writer, seed: true, CancellationToken.None);
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await writer.Products.Where(p => p.Id == 23).ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, "Uncommitted"));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var products = await client.GetFromJsonAsync<ProductResponse[]>("/api/v1/products", timeout.Token);
        Assert.Equal("Demo product 23", products![^1].Name);
        await transaction.RollbackAsync();
    }

    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:Mode", "SqlServer");
            builder.UseSetting("ConnectionStrings:Catalog", connection);
        });

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Reads { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }
}
