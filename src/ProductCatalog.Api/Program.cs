using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ProductCatalog.Api;
using ProductCatalog.Application;
using ProductCatalog.Infrastructure.DbContexts;
using ProductCatalog.Infrastructure.Seeding;
using ProductCatalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    if (builder.Configuration["Storage:Mode"] != "SqlServer")
    {
        throw new InvalidOperationException("The migration command requires Storage:Mode=SqlServer.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    await CatalogDatabaseSetup.InitializeAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>(),
        args.Contains("--seed", StringComparer.Ordinal), CancellationToken.None);
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUi(options =>
    {
        options.Path = "/swagger";
        options.SwaggerRoutes.Add(new("v1", "/openapi/v1.json"));
        options.SwaggerRoutes.Add(new("v2", "/openapi/v2.json"));
    });
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    // Liveness must remain independent of future SQL and broker readiness checks.
    Predicate = _ => false
});

app.MapControllers();

app.Run();
