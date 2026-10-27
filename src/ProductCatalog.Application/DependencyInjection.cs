using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Application.Services;

namespace ProductCatalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductApplicationService, ProductApplicationService>();

        return services;
    }
}
