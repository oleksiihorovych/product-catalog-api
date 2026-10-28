# Product Catalog API

A .NET REST API for listing products, viewing product details and updating descriptions, with SQL Server persistence and an optional in-memory demo mode.

## Prerequisites

- .NET SDK **10.0.402**, pinned in [global.json](global.json).
- A running **Microsoft SQL Server** instance for SQL mode. Mock mode and unit tests do not require a database.
- Optional: Visual Studio or VS Code with C# support for running and debugging from an IDE.

Run commands from the repository root. Environment examples use Bash/zsh; use equivalent environment settings in PowerShell or your IDE.

```sh
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
```

## Run everything with Docker Compose

Install and start Docker Desktop (or Docker Engine with Compose v2 on Linux).
The host does not need .NET or SQL Server installed. This setup is for local development:
SQL Server Developer edition, a local `sa` account and Development-only Swagger.

The host port defaults to `8080` and can be overridden with `API_PORT`.
The SQL password (`CatalogDemo2026!`) is set directly in `compose.yaml` for this local demo. No `.env` setup is required.
Production deployments must supply secrets outside this file. The SQL Server image
requires acceptance of its EULA; Compose sets `ACCEPT_EULA=Y` for this local instance.

Start the complete application:

```sh
docker compose up --build
```

Compose starts SQL Server, waits for a successful SQL query, runs the API image with
`--migrate --seed`, then starts the HTTP API only if that command succeeds. The
migration container exiting with code 0 is expected. The repeatable seed adds 23
demo products without overwriting existing rows. A failed migration blocks API
startup; inspect `docker compose logs migrate db`, fix the cause and rerun the command.
The normal API process still does not apply migrations.

- Swagger: <http://localhost:8080/swagger>
- Full catalog: <http://localhost:8080/api/v1/products>
- Page: <http://localhost:8080/api/v2/products?page=1&pageSize=10>
- Liveness: <http://localhost:8080/health/live> (process only, not SQL readiness).

Only the API port is published, on the host loopback interface. SQL is reachable
inside the Compose network at `db:1433`. To use another host port without editing files:

```sh
# Bash / zsh
API_PORT=9090 docker compose up --build
```

```powershell
# PowerShell
$env:API_PORT = '9090'
docker compose up --build
```

Swagger is then available at <http://localhost:9090/swagger>. The port inside the
API container remains 8080. With plain `docker run`, choose the host port using
`-p 9090:8080`.
The API image uses a multi-stage Release build with locked NuGet restore and runs
as the non-root .NET `app` user.

Useful commands:

```sh
docker compose ps -a
docker compose logs -f api
docker compose down
```

`down` removes containers and the network, while the named `catalog-data` volume
preserves the database. Start again with `docker compose up --build`. To deliberately
delete all local database data and reseed on the next start, use `docker compose down -v`.
Changing the password in `compose.yaml` does not change an existing database login: retain
the original password or explicitly change the SQL login (keep the connection string
and `MSSQL_SA_PASSWORD` in sync); reset the volume only if
its data can be discarded. Volumes are persistence, not backups.

**Architecture limitation:** SQL Server runs as `linux/amd64`. On Apple Silicon,
Docker Desktop needs amd64 emulation; Microsoft does not test or support SQL Server
under Rosetta/QEMU. A successful local run there is not evidence of supported
production deployment. Use an x86-64 Linux Docker host for a supported SQL container
architecture. See [Microsoft's SQL container requirements](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment).

Manual review checklist (separate from unit tests): start on a fresh volume, confirm
23 products and v2 pages of 10/10/3, PATCH a description, run `down` and start again,
and confirm the change persists and seed rows are not duplicated. Check Swagger
and the migration logs. Never reset a volume containing data you need.

## Run locally

### SQL mode (default)

Set the connection string for your local test database. Replace the server, port and credentials with your values:

```sh
export Storage__Mode=SqlServer
export ConnectionStrings__Catalog='Server=localhost,1433;Database=<your-db-name>;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=True'
```

For a container, use its published host port. `TrustServerCertificate=True` is for trusted local development only. Keep credentials outside Git.

Prepare the database and add sample products:

```sh
dotnet run --project src/ProductCatalog.Api --no-launch-profile -- --migrate --seed
```

This command applies EF Core migrations, inserts **23 demo products** and exits without starting the API. Repeating it adds only missing demo IDs and does not overwrite existing products. To apply migrations without sample data, omit `--seed`. The connection needs permissions to create/update the database schema and insert products.

Start the API in the same terminal:

```sh
dotnet run --project src/ProductCatalog.Api --launch-profile http
```

New SQL demo products have descriptions prefixed with `SQL mock data`; Mock-mode
products use `in-memory mock data`. Existing SQL rows, including edited descriptions,
are not overwritten by reseeding. To see the labels in an existing demo database,
use a fresh database; deleting a Compose volume with `docker compose down -v` destroys
all of its data.

Normal startup does not apply migrations or seed data. Changes persist across application restarts.

### Mock mode (no database)

```sh
dotnet run --project src/ProductCatalog.Api --launch-profile http -- --mock-data
```

Mock mode starts with the same 23 demo products and requires no connection string. Updates are shared within the running process and reset after restart.

`--mock-data` overrides `Storage:Mode`. Without the flag, SQL Server is the default; an explicitly configured `Storage__Mode=Mock` is still supported. Restart the application to switch modes.

The API runs at `http://localhost:5080`. In an IDE, select `ProductCatalog.Api`, the `http` profile and, for Mock mode, the `--mock-data` argument.

## API

Versions are selected through the URL path. Product IDs are nonempty GUIDs.

| Method | Path | Result |
| --- | --- | --- |
| GET | `/api/v1/products` | Complete product list as a JSON array; `[]` when empty |
| GET | `/api/v1/products/{id}` | Product details; `404` when not found |
| PATCH | `/api/v1/products/{id}` | Update only the description; `204 No Content` on success |
| GET | `/api/v2/products?page=1&pageSize=10` | A page with `items`, `page`, `pageSize` and `hasNext` |

Products contain `id`, `name`, `imgUri`, `price` (decimal) and nullable `description`.

v2 defaults to page **1** and page size **10**. Page size must be **1–100**; results are ordered by ID. Pages beyond the end return empty `items` and `hasNext: false`. v1 always returns the complete list, regardless of pagination query parameters.

PATCH accepts a JSON object containing only `description`, up to **4000 characters**:

```json
{"description":"Updated product description"}
```

The property is required; `null` clears it. Empty strings and whitespace are preserved. Unknown or duplicate properties are rejected. Invalid input returns `400`; errors use Problem Details with a `traceId`.

## API documentation

The `http` launch profile enables the Development environment:

- [Swagger UI](http://localhost:5080/swagger) — select v1 or v2 and try requests.
- [OpenAPI v1](http://localhost:5080/openapi/v1.json) and [OpenAPI v2](http://localhost:5080/openapi/v2.json) — machine-readable specifications.
- [ProductCatalog.http](ProductCatalog.http) — ready-to-run request examples.
- [Liveness](http://localhost:5080/health/live) — confirms the API process is running; it does not check database connectivity.

Swagger and OpenAPI are exposed only in Development.

## Unit tests

```sh
dotnet test --no-restore --configuration Release
```

Tests cover domain validation, application use cases, controller results, pagination and Mock repository behavior. They use isolated fakes and in-memory data, with no SQL Server or other external services. Live SQL behavior must be verified separately.

## Frameworks and libraries

| Technology | Purpose |
| --- | --- |
| .NET 10 / ASP.NET Core | Controller-based HTTP API, dependency injection, configuration and error handling |
| Entity Framework Core 10 with the SQL Server provider | Database queries, updates and schema migrations |
| FluentValidation | Product domain validation and shared description rules |
| Microsoft.AspNetCore.OpenApi | Generates versioned OpenAPI specifications from API metadata and XML comments |
| NSwag.AspNetCore | Hosts the Swagger UI |
| xUnit | Unit-test framework |
| Microsoft.NET.Test.Sdk / xunit.runner.visualstudio | Test discovery and execution through `dotnet test` and IDE test explorers |

The solution separates Domain models and validation, Application use cases and repository abstractions, Infrastructure adapters, and the API layer.
