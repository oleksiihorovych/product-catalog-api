# Product Catalog API

A .NET product catalog application with SQL Server persistence and an optional in-memory demo mode.

## Choose how to run

Run commands from the **repository root**: the `product-catalog-api` directory
containing `ProductCatalog.slnx` and `compose.yaml`. If you are in its parent
directory, run `cd product-catalog-api` first.

| Mode | Host requirements | Swagger |
| --- | --- | --- |
| Mock — quickest start | .NET SDK **10.0.402**; no SQL Server or Docker | <http://localhost:5080/swagger> |
| Docker Compose — API + SQL | Docker Desktop, or Docker Engine with Compose v2; no host .NET SDK or SQL installation | <http://localhost:8080/swagger> |
| Local API + existing SQL Server | .NET SDK **10.0.402** and a SQL Server connection | <http://localhost:5080/swagger> |

The SDK is pinned in [global.json](global.json). Visual Studio or VS Code with C#
support is optional. Environment examples use Bash/zsh; use equivalent settings in
PowerShell or your IDE.

## Quick start: Mock mode (no database)

With the .NET SDK installed, run:

```sh
dotnet restore --locked-mode
dotnet run --project src/ProductCatalog.Api --launch-profile http -- --mock-data
```

`dotnet run` builds and starts the API. Keep the terminal running, wait for
`Now listening on: http://localhost:5080`, then open
<http://localhost:5080/swagger>.
Press **Ctrl+C** to stop the API.

Mock mode requires no connection string. Updates are shared within the running
process and reset after restart. `--mock-data` overrides `Storage:Mode`; without
the flag, SQL Server is the default. `Storage__Mode=Mock` is also supported.

In an IDE, select `ProductCatalog.Api`, the `http` profile and the `--mock-data`
argument. Restart the application to switch storage modes.

If port 5080 is occupied, add `--urls http://localhost:5081` after `--mock-data`
and use <http://localhost:5081/swagger> instead.

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

The first run downloads images and packages and can take several minutes. Keep
the terminal open; **Ctrl+C** stops the foreground containers.

Compose starts SQL Server, waits for a successful SQL query, runs the API image with
`--migrate --seed`, then starts the HTTP API only if that command succeeds. The
migration container exiting with code 0 is expected. The repeatable seed adds 23
demo products without overwriting existing rows. A failed migration blocks API
startup; inspect `docker compose logs migrate db`, fix the cause and rerun the command.
The normal API process still does not apply migrations.

The application OpenAPI specification will be available at <http://localhost:8080/swagger>.

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

## Run locally with SQL Server (default storage)

Install the .NET SDK and restore packages from the repository root:

```sh
dotnet restore --locked-mode
```

Set the connection string for your local test database. Replace the server, port and credentials with your values:

```sh
export Storage__Mode=SqlServer
export ConnectionStrings__Catalog='Server=localhost,1433;Database=<your-db-name>;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=True'
```

For a container, use its published host port. The supplied Compose file does not
publish SQL Server's port, so its `db:1433` address is for containers on that network
only; it cannot be used by a host-run API. Use a separately reachable SQL instance
for this mode. `TrustServerCertificate=True` is for trusted local development only.
Keep credentials outside Git.

Prepare the database and add sample products:

```sh
dotnet run --project src/ProductCatalog.Api --no-launch-profile -- --migrate --seed
```

This command applies EF Core migrations, inserts **23 demo products** and exits without starting the API. Repeating it adds only missing demo IDs and does not overwrite existing products. To apply migrations without sample data, omit `--seed`. The connection needs permissions to create/update the database schema and insert products.

Start the API in the same terminal:

```sh
dotnet run --project src/ProductCatalog.Api --launch-profile http
```

Wait for `Now listening on: http://localhost:5080`, then open
<http://localhost:5080/swagger>. Stop with **Ctrl+C**.

New SQL demo products have descriptions prefixed with `SQL mock data`; Mock-mode
products use `in-memory mock data`. Existing SQL rows, including edited descriptions,
are not overwritten by reseeding. To see the labels in an existing demo database,
use a fresh database; deleting a Compose volume with `docker compose down -v` destroys
all of its data.

Normal startup does not apply migrations or seed data. Changes persist across application restarts.

## API documentation

The application OpenAPI specification will be available at <http://localhost:5080/swagger>.
For Docker Compose, use <http://localhost:8080/swagger>.
Swagger is available only in the Development environment, enabled by the `http`
launch profile and the supplied Compose configuration.

## Unit tests

The host needs the .NET SDK, even if you run the API with Docker. From the repository
root, run:

```sh
dotnet restore --locked-mode
dotnet test --no-restore --configuration Release
```

Unit tests use isolated fakes and in-memory data and require no SQL Server,
Docker or other external services.

## Continuous integration

[CI](.github/workflows/ci.yml) runs on pull requests targeting `main` and pushes
to `main`. Once the workflow is on the default branch, it can also be started
manually from GitHub Actions.

The workflow installs the SDK from `global.json`, caches NuGet packages using the
lock files, restores with `--locked-mode`, builds Release, runs only
`ProductCatalog.UnitTests`, and builds the Docker image for `linux/amd64`.
Test failures fail the job. The `unit-test-results` artifact contains TRX reports
when produced, including failed test runs, and is retained for seven days.

CI needs no database or repository secrets. The Docker step checks that the image
builds; image publication and deployment are outside this workflow.

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
