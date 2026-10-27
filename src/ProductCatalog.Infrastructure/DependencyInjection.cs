using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Infrastructure.DbContexts;
using ProductCatalog.Infrastructure.Repositories;

namespace ProductCatalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        switch (configuration["Storage:Mode"] ?? "Mock")
        {
            case "Mock":
                services.AddSingleton<IProductRepository, MockProductRepository>();
                break;

            case "SqlServer":
                services.AddSqlCatalogStorage(configuration);
                break;

            default:
                throw new InvalidOperationException("Storage:Mode must be Mock or SqlServer.");
        }

        return services;
    }

    private static IServiceCollection AddSqlCatalogStorage(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Catalog")
            ?? throw new InvalidOperationException("SqlServer mode requires ConnectionStrings:Catalog.");

        services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connection,
            sql => sql.CommandTimeout(120)));

        services.AddScoped<IProductRepository, SqlProductRepository>();

        return services;
    }
}
