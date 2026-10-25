using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ProductCatalog.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

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
