# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/MarketData.MockExchange/MarketData.MockExchange.csproj src/MarketData.MockExchange/
RUN dotnet restore src/MarketData.MockExchange/MarketData.MockExchange.csproj

COPY src/MarketData.MockExchange/ src/MarketData.MockExchange/
RUN dotnet publish src/MarketData.MockExchange/MarketData.MockExchange.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MarketData.MockExchange.dll"]
