FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/EnrichmentService/EnrichmentService.csproj src/EnrichmentService/
RUN dotnet restore src/EnrichmentService/EnrichmentService.csproj
COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/EnrichmentService/ src/EnrichmentService/
RUN dotnet publish src/EnrichmentService/EnrichmentService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EnrichmentService.dll"]
