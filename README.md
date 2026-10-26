# Product Catalog API

A .NET 10 LTS product catalog with a complete, streamed v1 listing, product lookup by GUID,
and SQL Server or Mock storage. Product descriptions can be updated with PATCH;
paginated v2 listing is planned separately.

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
| `GET /api/v1/products/{id}` | `200` with one product; invalid/empty GUID → `400`, unknown GUID → `404` |
| `PATCH /api/v1/products/{id}` | Change only description; `204` with no body, invalid input → `400`, missing product → `404`, unsupported media type → `415` |
| `/health/live` | `200 Healthy`, independent of database availability |
| `/swagger` | Local Swagger UI in Development |
| `/openapi/v1.json` | Generated OpenAPI document in Development |

Swagger/OpenAPI are disabled outside Development. NSwag provides the bundled UI;
ASP.NET Core generates the OpenAPI document from controller signatures and DTOs.
Controllers declare response codes and types with `ProducesResponseType`; XML comments
provide operation, parameter and response descriptions. Keep this convention for every
controller. Request and response schemas are generated from DTOs.
Configure `AllowedHosts` for other hostnames
and HTTPS when exposing the API outside the local development environment.

## Application structure

- **Domain:** Product invariants and a dedicated FluentValidation validator.
- **Application:** one scoped `IProductApplicationService` / `ProductApplicationService`
  for listing, lookup and description updates, using `IProductRepository`.
- **Persistence adapters:** EF Core SQL Server access, migrations and the Mock implementation.
- **API:** HTTP DTOs, streaming mapping, error handling and DI composition.
- **ProductCatalog.UnitTests:** isolated xUnit tests with fakes/stubs.

`Program.cs` calls `AddApplication()`, `AddInfrastructure(configuration)` and
`AddApi(configuration)`. Each layer owns its registrations. Application services use the
`ApplicationService` suffix; domain services use `DomainService` when domain logic requires
one. There is currently no separate domain service.

The product-specific repository exposes `ReadAllAsync` as a lazy `IAsyncEnumerable` and
`ReadByIdAsync` as a nullable product lookup. `UpdateDescriptionAsync` writes only Description
and reports whether a product matched. Controllers are organized by scenario:
`GetAllProductsController`, `GetProductByIdController` and `ChangeProductDescriptionController`.
It does not expose EF entities or `IQueryable` to HTTP consumers. Reads are projected without
tracking or full-catalog materialization. The context must remain alive during enumeration.

## HTTP and product contract

v1 always returns the full catalog as one JSON array, including when `page` or `pageSize`
query parameters are supplied. It has no hidden limit, export operation or broker dependency.

| Field | Contract |
| --- | --- |
| `id` | Nonempty GUID, serialized as a UUID string |
| `name` | Nonblank, at most 200 characters |
| `imgUri` | Absolute HTTP/HTTPS URI, at most 2048 characters |
| `price` | Nonnegative decimal(18,2), serialized as a JSON number |
| `description` | Nullable, at most 4000 characters |

IDs are supplied by the application; SQL does not generate them. SQL ordering uses `uniqueidentifier` order, which differs from .NET GUID comparison for arbitrary values. Mock uses the same SQL ordering.
The Product constructor rejects invalid state with `FluentValidation.ValidationException`;
errors contain field names and stable `Product.*` codes. Limits live in `DomainConstants`.
`ProductDescriptionRules` is shared by creation and the Application update scenario, so direct
service calls also validate the domain limit before writing.

Demo products use deterministic GUIDs `00000000-0000-0000-0000-000000000001` through
`00000000-0000-0000-0000-000000000017` (hexadecimal suffixes 1–23), names `Demo product 01` through `Demo product 23`, image URIs
`https://example.com/products/{number}.jpg` and prices `number * 10.25`. Every third description is null;
other descriptions are `Description for demo product {number:00}.`. The last demo product costs 235.75.

### Product lookup

`GET /api/v1/products/00000000-0000-0000-0000-000000000001` returns the same five fields
as that product in the full listing, including a nullable description. SQL lookup filters by
the primary key and projects a single read model; it does not enumerate the full catalog.
Mock reads from its in-process demo catalog. No brokers or credentials are needed for Mock.

Malformed or numeric IDs and the empty GUID return `400 ValidationProblemDetails`.
A valid GUID with no matching product returns `404 ProblemDetails`. Errors include `traceId`.
HTTP binding/validation runs before the action; Application also rejects `Guid.Empty` when
called directly. Successful lookup responses use `Cache-Control: no-store`; the bulk listing's
concurrency limit does not apply to lookup. GET endpoints remain public.

### Description update and manual check

PATCH is public and requires no authorization or version headers. Use Swagger's **Try it out**
or the prepared requests in `ProductCatalog.http`. In Mock, use product
`00000000-0000-0000-0000-000000000001`:

1. GET the card and note its fields.
2. PATCH with `{"description":"Manual review"}`: expect `204` and an empty response body.
3. GET again: description is `Manual review`; Id, Name, ImgUri and Price remain unchanged.
   GET the complete catalog: the same change is visible and all 23 products remain present.
4. Repeat the PATCH: expect `204`. PATCH `{"description":null}` to clear it, then GET to
   confirm null. Empty strings and whitespace are preserved as supplied.
5. Exercise the error cases below, then GET again to confirm rejected requests did not change data.

| PATCH input | Expected result |
| --- | --- |
| `{}` | `400`, description is required |
| `null` as the entire body | `400`, an object is required |
| `{"description":123}` | `400`, a string or null is required |
| `{"description":"New","price":0}` | `400`, unknown property |
| `{"description":"First","description":"Second"}` | `400`, duplicate property |
| A description longer than 4000 characters | `400` |
| Invalid or empty GUID | `400` |
| Unknown GUID `ffffffff-ffff-ffff-ffff-ffffffffffff` | `404` |
| `Content-Type: text/plain` | `415` |

Errors are Problem Details with `traceId`. Property names follow normal MVC case-insensitive
matching: `Description` is accepted, but supplying it alongside `description` is a duplicate.
The field must be present; an explicit null clears it. DTO attributes and object validation
handle the body rules locally, without a custom JSON converter or global JSON policy changes.

Application validates ID and the shared description rule, then makes one repository call.
SQL uses EF Core `ExecuteUpdateAsync` filtered by primary key, setting only Description;
there is no preliminary SELECT or readback. The last successful write wins. Mock serializes
writes and publishes stable snapshots of its small demo catalog. Changes are shared across
DI scopes in the process; restarting restores the demo data. Initial demo description for
product 1 is `Description for demo product 01.` if you want to restore it manually.

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
Exception messages and stack traces are not returned to clients. GET and PATCH are public;
authorization is outside the current delivery scope.

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

Switch to a runtime connection with SELECT permission on `dbo.Products` and UPDATE permission
on its `Description` column, then start the API
using the quick-start command. Normal HTTP startup never migrates or seeds. SQL listing uses one
ordered query against the configured primary; lookup uses a separate primary-key query.
Do not enable EF retry-on-failure for the full listing without evaluating its full-result buffering. The configured command timeout is 120 seconds.
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
controller output, GUID validation, description rules and targeted update delegation, Mock
update/lookup/listing parity and scoped visibility, lazy mapping, cancellation/disposal, source failures, exception handling
and rate-limit error fallback. Shared sample values live in `TestConstants`.

Unit tests do not prove routing, generated OpenAPI, SQL behavior, transport failure handling
or process-memory bounds. Verify those manually against the running application:

- In Mock mode, request v1 and confirm 23 unique IDs in order, all five fields and the last price.
- Repeat with `?page=1&pageSize=10`; v1 must still return all 23 products.
- Request `/api/v1/products/00000000-0000-0000-0000-000000000001`: expect `200`, the same
  fields as in the list and `Cache-Control: no-store`. Repeat with suffix `000000000003` for
  a null description and `000000000017` for the last product.
- Request `/api/v1/products/ffffffff-ffff-ffff-ffff-ffffffffffff`: expect `404`.
- Request an empty GUID, `abc` or `1`: expect `400`. Verify errors are
  `application/problem+json` with `traceId`, and empty GUID errors identify `id`.
- Check Swagger describes the lookup ID as `string/uuid` and lists `200/400/404/500`.
- Follow the description-update checklist above; Swagger must list `204/400/404/415/500`,
  and the `204` response must have no content schema.
- Open health, Swagger and OpenAPI; request an unknown route with `Accept: application/problem+json`.
- On a separate SQL database, migrate/seed twice and verify the same catalog using a read-only login.
- On another fresh migrated database without seed, confirm `200` and `[]`.
- Under saturated catalog admission, verify `429` for JSON and plain-text clients and independent liveness.
- Check initial storage failures return safe errors; discard incomplete responses after transfer failures.

A one-time local Mock HTTP check verified all 23 lookup responses against the listing,
invalid/empty/numeric IDs, missing products, cache headers, OpenAPI, Swagger UI and health.
This is a manual acceptance check, not an automated HTTP test suite. SQL lookup and the GUID
migration have not been executed against a live database. The SQL description UPDATE is
also unverified against a live SQL Server. A separate Mock PATCH check confirmed bodyless
204, repeated assignments, null/empty/whitespace, the 4000-character boundary, unchanged other
fields, validation errors, lookup/list visibility and generated OpenAPI. These manual checks
are not an automated HTTP test suite. Production capacity and memory bounds require separate measurements.

A one-time local Mock PATCH check verified bodyless 204, repeated assignment, clearing,
empty/whitespace and Unicode values, length boundaries, unchanged remaining fields, visibility
in lookup and listing, validation errors and OpenAPI/Swagger. SQL description updates have
not been executed against a live SQL Server; Mock and unit checks do not establish SQL runtime behavior.

## GUID identifier upgrade

Product IDs now use `Guid` / SQL Server `uniqueidentifier`; numeric-ID clients must migrate.
The original migration is retained. The GUID migration preserves every product and maps the
old positive bigint to its hexadecimal value in the last 16 UUID digits, for example
`23` becomes `00000000-0000-0000-0000-000000000017`. Seed uses the same mapping and remains
repeatable. Existing ID order may change; no numeric-ID compatibility endpoint is provided.
Back up an existing database before applying migrations. This migration is forward-only:
rollback requires restoring the backup with the matching application version. SQL execution
of this upgrade has not been verified against a live database.
