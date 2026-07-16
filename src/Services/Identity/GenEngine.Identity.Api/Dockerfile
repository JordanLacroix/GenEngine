FROM mcr.microsoft.com/dotnet/sdk:10.0.103-alpine3.22@sha256:bb9890f2f2dbefa91b161feecfe6f85fd4c8c9e3933158c54287f37f611d5ed5 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/Identity/GenEngine.Identity.Api/GenEngine.Identity.Api.csproj --force-evaluate
RUN dotnet publish src/Services/Identity/GenEngine.Identity.Api/GenEngine.Identity.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:27b6b84beeede74fd16886177d360799c8e4299ceadfbd64eef57bafead7878a AS final
WORKDIR /app
COPY --from=build --chown=app:app /app .
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/health/ready > /dev/null || exit 1
ENTRYPOINT ["dotnet", "GenEngine.Identity.Api.dll"]
