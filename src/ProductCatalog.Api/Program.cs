using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ProductCatalog.Api.Middleware;
using ProductCatalog.Api.Products;
using ProductCatalog.Infrastructure;
using ProductCatalog.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddCatalogStorage(builder.Configuration);
builder.Services.AddProductListing(builder.Configuration);
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    if (builder.Configuration["Storage:Mode"] != "SqlServer")
        throw new InvalidOperationException("The migration command requires Storage:Mode=SqlServer.");
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
    });
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    // Liveness must remain independent of future SQL and broker readiness checks.
    Predicate = _ => false
});

app.MapControllers();

app.Run();

/// <summary>Exposes the entry point to the API integration test host.</summary>
public partial class Program;
