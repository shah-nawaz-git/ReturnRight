# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY ReturnRight.slnx ./
COPY src/ReturnRight.Api/ReturnRight.Api.csproj src/ReturnRight.Api/
COPY tests/ReturnRight.UnitTests/ReturnRight.UnitTests.csproj tests/ReturnRight.UnitTests/
COPY tests/ReturnRight.IntegrationTests/ReturnRight.IntegrationTests.csproj tests/ReturnRight.IntegrationTests/
RUN dotnet restore src/ReturnRight.Api/ReturnRight.Api.csproj

COPY . .
RUN dotnet publish src/ReturnRight.Api/ReturnRight.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       libfontconfig1 libfreetype6 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

RUN useradd -m -u 1000 appuser \
    && mkdir -p /data/storage \
    && chown -R appuser:appuser /data
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ReturnRight.Api.dll"]
