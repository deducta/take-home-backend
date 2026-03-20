FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/MockAiApi/MockAiApi.csproj src/MockAiApi/
RUN dotnet restore src/MockAiApi/MockAiApi.csproj
COPY src/MockAiApi/ src/MockAiApi/
RUN dotnet publish src/MockAiApi/MockAiApi.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MockAiApi.dll"]
