# Multi-stage Dockerfile for CashFlow.Consolidation.Api (.NET 9)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for optimized layer caching
COPY src/Shared/CashFlow.Shared.Contracts/CashFlow.Shared.Contracts.csproj src/Shared/CashFlow.Shared.Contracts/
COPY src/Consolidation/CashFlow.Consolidation.Domain/CashFlow.Consolidation.Domain.csproj src/Consolidation/CashFlow.Consolidation.Domain/
COPY src/Consolidation/CashFlow.Consolidation.Application/CashFlow.Consolidation.Application.csproj src/Consolidation/CashFlow.Consolidation.Application/
COPY src/Consolidation/CashFlow.Consolidation.Infrastructure/CashFlow.Consolidation.Infrastructure.csproj src/Consolidation/CashFlow.Consolidation.Infrastructure/
COPY src/Consolidation/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj src/Consolidation/CashFlow.Consolidation.Api/

# Restore dependencies
RUN dotnet restore src/Consolidation/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj

# Copy source code
COPY src/Shared/ src/Shared/
COPY src/Consolidation/ src/Consolidation/

# Publish
WORKDIR /src/src/Consolidation/CashFlow.Consolidation.Api
RUN dotnet publish CashFlow.Consolidation.Api.csproj -c Release -o /app/publish

# Runtime image (.NET 9 ASP.NET non-root)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "CashFlow.Consolidation.Api.dll"]
