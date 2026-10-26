# Multi-stage Dockerfile for CashFlow.Consolidation.Api (.NET 10 LTS)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for optimized layer caching
COPY src/Shared/CashFlow.Shared.Contracts/CashFlow.Shared.Contracts.csproj src/Shared/CashFlow.Shared.Contracts/
COPY src/Aspire/CashFlow.ServiceDefaults/CashFlow.ServiceDefaults.csproj src/Aspire/CashFlow.ServiceDefaults/
COPY src/Consolidation/CashFlow.Consolidation.Domain/CashFlow.Consolidation.Domain.csproj src/Consolidation/CashFlow.Consolidation.Domain/
COPY src/Consolidation/CashFlow.Consolidation.Application/CashFlow.Consolidation.Application.csproj src/Consolidation/CashFlow.Consolidation.Application/
COPY src/Consolidation/CashFlow.Consolidation.Infrastructure/CashFlow.Consolidation.Infrastructure.csproj src/Consolidation/CashFlow.Consolidation.Infrastructure/
COPY src/Consolidation/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj src/Consolidation/CashFlow.Consolidation.Api/

# Restore dependencies
RUN dotnet restore src/Consolidation/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj

# Copy source code
COPY src/Shared/ src/Shared/
COPY src/Aspire/ src/Aspire/
COPY src/Consolidation/ src/Consolidation/

# Publish
WORKDIR /src/src/Consolidation/CashFlow.Consolidation.Api
RUN dotnet publish CashFlow.Consolidation.Api.csproj -c Release -o /app/publish

# Runtime image (.NET 10 LTS ASP.NET non-root)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CashFlow.Consolidation.Api.dll"]
