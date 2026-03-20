FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/IngestionService/IngestionService.csproj src/IngestionService/
RUN dotnet restore src/IngestionService/IngestionService.csproj
COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/IngestionService/ src/IngestionService/
RUN dotnet publish src/IngestionService/IngestionService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IngestionService.dll"]
