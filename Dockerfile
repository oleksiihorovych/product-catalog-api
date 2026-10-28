FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ProductCatalog.Domain/*.csproj src/ProductCatalog.Domain/packages.lock.json src/ProductCatalog.Domain/
COPY src/ProductCatalog.Application/*.csproj src/ProductCatalog.Application/packages.lock.json src/ProductCatalog.Application/
COPY src/ProductCatalog.Infrastructure/*.csproj src/ProductCatalog.Infrastructure/packages.lock.json src/ProductCatalog.Infrastructure/
COPY src/ProductCatalog.Api/*.csproj src/ProductCatalog.Api/packages.lock.json src/ProductCatalog.Api/
RUN dotnet restore src/ProductCatalog.Api/ProductCatalog.Api.csproj --locked-mode

COPY src/ src/
RUN dotnet publish src/ProductCatalog.Api/ProductCatalog.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ProductCatalog.Api.dll"]
