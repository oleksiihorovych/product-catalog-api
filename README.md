# Product Catalog API

An ASP.NET Core API with a layered architecture, targeting .NET 10 LTS.

## Prerequisites

- .NET SDK 10.0.402 (the version is pinned in `global.json`; newer patches in the same feature band are accepted).
- An editor with .NET support, such as Visual Studio Code.

## Structure

```text
src/
  ProductCatalog.Domain/          Domain model and rules
  ProductCatalog.Application/     Use cases and boundary contracts
  ProductCatalog.Infrastructure/  Persistence and external adapters
  ProductCatalog.Api/             HTTP host and dependency composition
```

Application references Domain. Infrastructure references Application and Domain.
Api references Application and Infrastructure for composition. The library
projects are intentionally empty until the first functional slice needs them.

## Build

Run these commands from the repository root:

```sh
dotnet restore
dotnet build --no-restore
```

## Run

```sh
dotnet run --project src/ProductCatalog.Api --launch-profile http
```

The host listens on `http://localhost:5080`. Product endpoints are not implemented
yet. No database, containers, or message brokers are needed to start the host.
