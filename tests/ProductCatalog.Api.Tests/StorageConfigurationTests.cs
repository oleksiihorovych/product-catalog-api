using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Infrastructure.Persistence;
using Xunit;

namespace ProductCatalog.Api.Tests;

public sealed class StorageConfigurationTests
{
    [Fact]
    public async Task SqlMode_RegistersSqlAdapterWhileLivenessNeedsNoConnection()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:Mode", "SqlServer");
            builder.UseSetting("ConnectionStrings:Catalog", "Server=localhost;Database=NotConnected;Integrated Security=true");
        });
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetService<CatalogDbContext>());
        using var response = await client.GetAsync("/health/live");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
