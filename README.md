# Product Catalog API

A .NET 10 LTS product catalog with a complete, streamed v1 listing and SQL Server or Mock storage.
Product lookup, description updates and paginated v2 listing are planned separately.

## Build and run

Use .NET SDK 10.0.402, pinned in `global.json`. Run from the repository root:

```sh
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
dotnet run --project src/ProductCatalog.Api --launch-profile http
```

The default Mock mode has 23 deterministic products and requires no external services.
Open `http://localhost:5080/swagger`; request examples are in `ProductCatalog.http`.

| Route | Result |
| --- | --- |
| `GET /api/v1/products` | `200`, every product ordered by ID; an empty catalog returns `[]` |
| `/health/live` | `200 Healthy`, independent of database availability |
| `/swagger` | Local Swagger UI in Development |
| `/openapi/v1.json` | Generated OpenAPI document in Development |

Swagger/OpenAPI are disabled outside Development. NSwag provides the bundled UI;
ASP.NET Core generates the OpenAPI document. Configure `AllowedHosts` for other hostnames
and HTTPS when exposing the API outside the local development environment.

## Application structure

- **Domain:** Product invariants and a dedicated FluentValidation validator.
- **Application:** `ProductCatalogApplicationService` and the `IProductRepository` boundary.
- **Persistence adapters:** EF Core SQL Server access, migrations and the Mock implementation.
- **API:** HTTP DTOs, streaming mapping, error handling and DI composition.
- **ProductCatalog.UnitTests:** isolated xUnit tests with fakes/stubs.

`Program.cs` calls `AddApplication()`, `AddInfrastructure(configuration)` and
`AddApi(configuration)`. Each layer owns its registrations. Application services use the
`ApplicationService` suffix; domain services use `DomainService` when domain logic requires
one. There is currently no separate domain service.

The product-specific repository exposes `ReadAllAsync` as a lazy `IAsyncEnumerable`.
It does not expose EF entities or `IQueryable` to HTTP consumers. Reads are projected without
tracking or full-catalog materialization. The context must remain alive during enumeration.

## HTTP and product contract

v1 always returns the full catalog as one JSON array, including when `page` or `pageSize`
query parameters are supplied. It has no hidden limit, export operation or broker dependency.

| Field | Contract |
| --- | --- |
| `id` | Positive signed 64-bit integer |
| `name` | Nonblank, at most 200 characters |
| `imgUri` | Absolute HTTP/HTTPS URI, at most 2048 characters |
| `price` | Nonnegative decimal(18,2), serialized as a JSON number |
| `description` | Nullable, at most 4000 characters |

Clients must preserve 64-bit IDs; large IDs can exceed JavaScript's safe integer range.
The Product constructor rejects invalid state with `FluentValidation.ValidationException`;
errors contain field names and stable `Product.*` codes. Limits live in `DomainConstants`.

Demo products use IDs 1–23, names `Demo product 01` through `Demo product 23`, image URIs
`https://example.com/products/{id}.jpg` and prices `id * 10.25`. Every third description is null;
other descriptions are `Description for demo product {id:00}.`. ID 23 costs 235.75.

## Streaming, limits and errors

The controller returns `IAsyncEnumerable<ProductResponse>` through an API mapper.
ASP.NET Core/System.Text.Json manage incremental serialization and flush boundaries.
Cancellation reaches the repository; disposing enumeration releases SQL read resources.
Clients must consume the complete response: receiving `200` headers alone does not prove a
successful full transfer. Failures after transmission starts cannot become a new error document.

`Catalog__MaxConcurrentListings` defaults to 4 per process, accepts 1–128 and has no waiting queue.
These values are for demonstration; meaningful production limits require real traffic,
duration and resource measurements. Excess requests receive `429` Problem Details, with a
plain-text fallback when the writer cannot satisfy the Accept header. Liveness bypasses this
policy. Successful catalog responses use `Cache-Control: no-store`.

Unexpected errors before the response starts return safe `500` Problem Details with `traceId`.
Exception messages and stack traces are not returned to clients. GET is public; write
endpoints and their authorization are not implemented yet.

## SQL Server setup

Set `Storage__Mode=Mock` or `Storage__Mode=SqlServer`. SQL mode requires
`ConnectionStrings__Catalog`, supplied through environment variables or another external
configuration provider. Never commit credentials. Connect to an existing SQL Server instance.

For a dedicated development database, configure SQL mode and an administrative connection:

```sh
dotnet run --project src/ProductCatalog.Api --no-launch-profile -- --migrate --seed
```

The command applies migrations, enables `READ_COMMITTED_SNAPSHOT`, inserts missing demo IDs
and exits without serving HTTP. Repeating seed does not overwrite existing products. Omit
`--seed` when preparing a fresh empty database. Migrations precede the seed transaction;
a seed failure does not undo applied schema changes. Schedule migration without competing
connections; it does not force-disconnect sessions.

Switch to a runtime connection with SELECT permission on `dbo.Products`, then start the API
using the quick-start command. Normal HTTP startup never migrates or seeds. SQL reads use one
ordered query against the configured primary; do not enable EF retry-on-failure on this path
without evaluating its full-result buffering. The configured command timeout is 120 seconds.
Choose connection-pool and proxy limits using measurements.

Use encrypted SQL connections. `TrustServerCertificate=true` is only suitable for a trusted
local development server; production requires certificate validation. Use a SQL Server instance supported on its operating system and architecture.

EF tooling is pinned in `dotnet-tools.json`. With SQL mode and connection configured:

```sh
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/ProductCatalog.Infrastructure --startup-project src/ProductCatalog.Api
```

## Unit tests and manual verification

The solution contains unit tests only; tests run without an HTTP host, database or broker. Tests cover domain invariants, service/repository delegation, complete
controller output, lazy mapping, cancellation/disposal, source failures, exception handling
and rate-limit error fallback. Shared sample values live in `TestConstants`.

Unit tests do not prove routing, generated OpenAPI, SQL behavior, transport failure handling
or process-memory bounds. Verify those manually against the running application:

- In Mock mode, request v1 and confirm 23 unique IDs in order, all five fields and the last price.
- Repeat with `?page=1&pageSize=10`; v1 must still return all 23 products.
- Open health, Swagger and OpenAPI; request an unknown route with `Accept: application/problem+json`.
- On a separate SQL database, migrate/seed twice and verify the same catalog using a read-only login.
- On another fresh migrated database without seed, confirm `200` and `[]`.
- Under saturated catalog admission, verify `429` for JSON and plain-text clients and independent liveness.
- Check initial storage failures return safe errors; discard incomplete responses after transfer failures.

Production capacity and memory bounds require separate measurements.
