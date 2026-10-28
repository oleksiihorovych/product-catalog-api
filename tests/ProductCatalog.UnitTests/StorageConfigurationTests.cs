using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Infrastructure;
using ProductCatalog.Infrastructure.Repositories;
using Xunit;

namespace ProductCatalog.UnitTests;

public sealed class StorageConfigurationTests
{
    [Fact]
    public void AddInfrastructure_DefaultsToSqlWithScopedRepository()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = "Server=localhost;Database=ConfigurationOnly;Integrated Security=true"
            }).Build();

        services.AddInfrastructure(configuration);

        var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IProductRepository));
        Assert.Equal(typeof(SqlProductRepository), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfrastructure_DefaultSqlRequiresConnectionWithoutFallingBackToMock(string? connection)
    {
        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Catalog"] = connection }).Build()));
        Assert.Equal("SqlServer mode requires ConnectionStrings:Catalog.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sqlserver")]
    [InlineData("Unknown")]
    public void AddInfrastructure_RejectsUnsupportedModeWithoutRegisteringRepository(string mode)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Storage:Mode"] = mode }).Build();

        Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IProductRepository));
    }
}
