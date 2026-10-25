FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ProductCatalog.Domain/*.csproj src/ProductCatalog.Domain/packages.lock.json src/ProductCatalog.Domain/
COPY src/ProductCatalog.Application/*.csproj src/ProductCatalog.Application/packages.lock.json src/ProductCatalog.Application/
COPY src/ProductCatalog.Infrastructure/*.csproj src/ProductCatalog.Infrastructure/packages.lock.json src/ProductCatalog.Infrastructure/
COPY src/ProductCatalog.Api/*.csproj src/ProductCatalog.Api/packages.lock.json src/ProductCatalog.Api/
RUN dotnet restore src/ProductCatalog.Api --locked-mode
COPY src/ src/
RUN dotnet publish src/ProductCatalog.Api --no-restore --configuration Release -p:UseAppHost=false --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca AS final
WORKDIR /app
RUN apt-get update && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "ProductCatalog.Api.dll"]
