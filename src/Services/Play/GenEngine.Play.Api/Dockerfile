FROM mcr.microsoft.com/dotnet/sdk:10.0.103-alpine3.22 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/Play/GenEngine.Play.Api/GenEngine.Play.Api.csproj --force-evaluate
RUN dotnet publish src/Services/Play/GenEngine.Play.Api/GenEngine.Play.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build --chown=app:app /app .
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/health/ready > /dev/null || exit 1
ENTRYPOINT ["dotnet", "GenEngine.Play.Api.dll"]
