# Build from the repository root:
#   docker build -f src/Catalog.Api/Dockerfile -t catalog .

# ---------- Stage 1: build (large image with the SDK, thrown away afterwards) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /repo

# Copy only the project files first. Docker caches this layer, so packages are
# downloaded again only when a project file changes, not on every code change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/MicroShop.ServiceDefaults/MicroShop.ServiceDefaults.csproj src/MicroShop.ServiceDefaults/
COPY src/Catalog.Api/Catalog.Api.csproj src/Catalog.Api/
RUN dotnet restore src/Catalog.Api/Catalog.Api.csproj

COPY src/MicroShop.ServiceDefaults/ src/MicroShop.ServiceDefaults/
COPY src/Catalog.Api/ src/Catalog.Api/
RUN dotnet publish src/Catalog.Api/Catalog.Api.csproj -c Release --no-restore -o /app

# ---------- Stage 2: run (small image: no SDK, no shell, not root) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .

# The pipeline passes the git commit, so a running container can report its version.
ARG APP_VERSION=local
ENV APP_VERSION=$APP_VERSION

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
