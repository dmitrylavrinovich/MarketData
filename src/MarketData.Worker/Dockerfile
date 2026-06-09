# syntax=docker/dockerfile:1

# --- build: publish Worker (runtime artifact) ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/MarketData.Domain/MarketData.Domain.csproj src/MarketData.Domain/
COPY src/MarketData.Application/MarketData.Application.csproj src/MarketData.Application/
COPY src/MarketData.Infrastructure/MarketData.Infrastructure.csproj src/MarketData.Infrastructure/
COPY src/MarketData.Worker/MarketData.Worker.csproj src/MarketData.Worker/
RUN dotnet restore src/MarketData.Worker/MarketData.Worker.csproj

COPY src/MarketData.Domain/ src/MarketData.Domain/
COPY src/MarketData.Application/ src/MarketData.Application/
COPY src/MarketData.Infrastructure/ src/MarketData.Infrastructure/
COPY src/MarketData.Worker/ src/MarketData.Worker/
RUN dotnet publish src/MarketData.Worker/MarketData.Worker.csproj -c Release -o /app/publish --no-restore

# --- migrate: SDK + dotnet-ef (one-shot init service in compose) ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS migrate
WORKDIR /src

COPY .config/dotnet-tools.json .config/
COPY src/MarketData.Domain/MarketData.Domain.csproj src/MarketData.Domain/
COPY src/MarketData.Application/MarketData.Application.csproj src/MarketData.Application/
COPY src/MarketData.Infrastructure/MarketData.Infrastructure.csproj src/MarketData.Infrastructure/
COPY src/MarketData.Worker/MarketData.Worker.csproj src/MarketData.Worker/
RUN dotnet tool restore

COPY src/MarketData.Domain/ src/MarketData.Domain/
COPY src/MarketData.Application/ src/MarketData.Application/
COPY src/MarketData.Infrastructure/ src/MarketData.Infrastructure/
COPY src/MarketData.Worker/ src/MarketData.Worker/

# --- runtime: slim aspnet image for aggregator ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MarketData.Worker.dll"]
