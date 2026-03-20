FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/PersistenceService/PersistenceService.csproj src/PersistenceService/
RUN dotnet restore src/PersistenceService/PersistenceService.csproj
COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/PersistenceService/ src/PersistenceService/
RUN dotnet publish src/PersistenceService/PersistenceService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PersistenceService.dll"]
