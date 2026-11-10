# syntax=docker/dockerfile:1

# 1. Angular build
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npx ng build --configuration production

# 2. .NET publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/JaCrm.Api/JaCrm.Api.csproj src/JaCrm.Api/
RUN dotnet restore src/JaCrm.Api/JaCrm.Api.csproj
COPY src/ src/
RUN dotnet publish src/JaCrm.Api/JaCrm.Api.csproj -c Release -o /app --no-restore

# 3. Runtime: one process serves the API and the compiled Angular app
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist/web/browser ./wwwroot/
# The database, data-protection keys and backups live on a volume mounted at /data.
RUN mkdir -p /data && chown "$APP_UID" /data
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    App__DataDirectory=/data
EXPOSE 8080
VOLUME ["/data"]
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD ["dotnet", "JaCrm.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "JaCrm.Api.dll"]
