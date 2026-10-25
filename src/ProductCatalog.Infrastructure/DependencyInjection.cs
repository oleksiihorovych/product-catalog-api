using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Application.Products;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Infrastructure.Products;

namespace ProductCatalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IProductCatalogService, ProductCatalogService>();
        switch (configuration["Storage:Mode"] ?? "Mock")
        {
            case "Mock":
                services.AddSingleton<IProductReader, MockProductReader>();
                break;
            case "SqlServer":
                var connection = configuration.GetConnectionString("Catalog")
                    ?? throw new InvalidOperationException("SqlServer mode requires ConnectionStrings:Catalog.");
                services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connection,
                    sql => sql.CommandTimeout(120)));
                services.AddScoped<IProductReader, SqlProductReader>();
                break;
            default:
                throw new InvalidOperationException("Storage:Mode must be Mock or SqlServer.");
        }

        return services;
    }
}
