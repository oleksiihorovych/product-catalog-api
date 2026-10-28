# Product Catalog API

A .NET product catalog application.

Supports two versions of local run: 

1. Start the containers with Docker
2. Run the API locally with the .NET SDK. Both support two storage modes:

- **SQL Server (default):** products are stored in a database and changes persist
  across application restarts.
- **Mock:** 23 demo products are kept in memory. No database or connection string
  is needed; changes reset when the API process restarts.

Run all commands from the **repository root**, `product-catalog-api`, containing
`ProductCatalog.slnx` and `compose.yaml`. Environment examples use Bash/zsh.

All four options below open Swagger at <http://localhost:5080/swagger>. Run one
option at a time so port `5080` is free. Visual Studio or VS Code with C# support
is optional for local development.

## Docker: SQL Server (default)

Prerequisites:

- Docker Desktop installed and running, or Docker Engine with Compose v2 on Linux.

Start the API and SQL Server with one command:

```sh
docker compose up --build
```

The host needs neither the .NET SDK nor a separate SQL Server installation.
Compose supplies the connection string, starts SQL Server, waits for it to become
healthy, applies migrations, seeds 23 demo products, and starts the API. The
migration container exiting with code `0` is expected; a failed migration blocks
API startup. The first run downloads images and packages and can take several minutes.

Open <http://localhost:5080/swagger> when the API starts. Press **Ctrl+C** to stop
foreground containers. To remove the stopped containers and network, run:

```sh
docker compose down
```

The named `catalog-data` volume preserves SQL data. Repeating the startup command
adds only missing demo products and does not overwrite existing rows.
`docker compose down -v` deliberately deletes all local database data; the next
startup creates and seeds a fresh database. Volumes are persistence, not backups.

This is a local development setup: SQL Server Developer edition, a local `sa`
account, and Development-only Swagger. The demo password (`CatalogDemo2026!`)
is set in `compose.yaml`; no `.env` file or host connection string is required.
Compose accepts the SQL Server EULA with `ACCEPT_EULA=Y`. Production deployments
must supply secrets separately. Editing the password in Compose does not update
an existing SQL login; retain the original password or change the login and both
Compose password settings together.

Only the API port is published, bound to `127.0.0.1`. SQL Server is accessible
inside the Compose network at `db:1433`. To use another host port:

```sh
API_PORT=9090 docker compose up --build
```

Then open <http://localhost:9090/swagger>. The API container listens on `8080`;
Compose maps host port `5080` to it by default. The image uses a multi-stage
Release build with locked NuGet restore and runs as the non-root .NET `app` user.

For startup diagnostics:

```sh
docker compose ps -a
docker compose logs -f api
docker compose logs migrate db
```

## Docker: Mock data

Prerequisites:

- Docker Desktop installed and running, or Docker Engine with Compose v2 on Linux.
- No SQL Server installation, connection string, or host .NET SDK is required.

Start just the API with in-memory demo data using one command:

```sh
docker compose run --build --rm --no-deps --service-ports api --mock-data
```

`--no-deps` skips the SQL Server and migration services; `--service-ports` publishes
the API on `5080`. The `--mock-data` argument overrides the SQL storage setting in
Compose. The connection string in Compose is ignored in this mode.

Open <http://localhost:5080/swagger>. Press **Ctrl+C** to stop the API; `--rm`
removes this container on exit. Its data changes are lost. If you previously
started the SQL setup, stop it with `docker compose down` before switching to
this option. `API_PORT` can override the host port here as well.

## Local: SQL Server (default)

Prerequisites:

- .NET SDK **10.0.402**, pinned in [global.json](global.json).
- A running SQL Server instance reachable from your machine.
- A connection string set as the **`ConnectionStrings__Catalog` environment
  variable**. Database setup requires permissions to create/update the schema
  and insert demo products.

Restore dependencies:

```sh
dotnet restore --locked-mode
```

Set the environment variables in the terminal where you will run the API.
Replace the server, database name, and credentials with your own values.

Bash/zsh:

```sh
export Storage__Mode=SqlServer
export ConnectionStrings__Catalog='Server=localhost,1433;Database=<your-db-name>;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=True'
```

PowerShell:

```powershell
$env:Storage__Mode = 'SqlServer'
$env:ConnectionStrings__Catalog = 'Server=localhost,1433;Database=<your-db-name>;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=True'
```

The double underscore maps `ConnectionStrings__Catalog` to the .NET configuration
key `ConnectionStrings:Catalog`. Keep credentials outside Git.
`TrustServerCertificate=True` is for trusted local development only.

The supplied Compose file does not publish SQL Server's port. Its `db:1433`
address is internal to Docker and cannot be used by a locally running API;
use a separately reachable SQL instance for this option.

Prepare the database and sample data:

```sh
dotnet run --project src/ProductCatalog.Api --no-launch-profile -- --migrate --seed
```

This applies EF Core migrations, adds 23 demo products, and exits. Repeating it
adds only missing demo IDs without overwriting existing rows. Omit `--seed` to
apply migrations without sample data. Normal API startup does not run migrations
or seed the database.

Start the API in the **same terminal**, so it inherits the environment variables:

```sh
dotnet run --project src/ProductCatalog.Api --launch-profile http
```

Wait for `Now listening on: http://localhost:5080`, then open
<http://localhost:5080/swagger>. Press **Ctrl+C** to stop the API. Changes are
saved in SQL Server. New SQL demo descriptions start with `SQL mock data`;
existing descriptions are not overwritten by reseeding.

For an IDE launch, set `ConnectionStrings__Catalog` and `Storage__Mode=SqlServer`
in its run configuration and select the `http` profile. Variables exported in a
terminal are inherited only by processes launched from that terminal.

## Local: Mock data

Prerequisites:

- .NET SDK **10.0.402**, pinned in [global.json](global.json).
- No SQL Server, connection string, or Docker is required.

Restore dependencies and start the API:

```sh
dotnet restore --locked-mode
dotnet run --project src/ProductCatalog.Api --launch-profile http -- --mock-data
```

Wait for `Now listening on: http://localhost:5080`, then open
<http://localhost:5080/swagger>. Press **Ctrl+C** to stop the API.

`--mock-data` overrides `Storage:Mode`, including a SQL setting from a previous
run. `Storage__Mode=Mock` is also supported. Mock demo descriptions start with
`in-memory mock data`; updates are shared within the running process and reset
on restart.

In an IDE, select `ProductCatalog.Api`, the `http` profile, and the `--mock-data`
argument. To use another local port, append `--urls http://localhost:5081` to the
run command and open <http://localhost:5081/swagger>.

## API documentation

Swagger at <http://localhost:5080/swagger> describes the API contract. It is
available only in the Development environment, enabled by the local `http`
launch profile and the Docker commands above.

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
