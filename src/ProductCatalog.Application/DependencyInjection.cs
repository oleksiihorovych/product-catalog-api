using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Application.Products;

namespace ProductCatalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductCatalogApplicationService, ProductCatalogApplicationService>();

        return services;
    }
}
