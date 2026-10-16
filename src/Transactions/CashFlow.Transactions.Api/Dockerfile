# Multi-stage Dockerfile for CashFlow.Transactions.Api (.NET 9)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and csproj files for layer caching
COPY CashFlow.sln ./
COPY src/Shared/CashFlow.Shared.Contracts/CashFlow.Shared.Contracts.csproj src/Shared/CashFlow.Shared.Contracts/
COPY src/Transactions/CashFlow.Transactions.Domain/CashFlow.Transactions.Domain.csproj src/Transactions/CashFlow.Transactions.Domain/
COPY src/Transactions/CashFlow.Transactions.Application/CashFlow.Transactions.Application.csproj src/Transactions/CashFlow.Transactions.Application/
COPY src/Transactions/CashFlow.Transactions.Infrastructure/CashFlow.Transactions.Infrastructure.csproj src/Transactions/CashFlow.Transactions.Infrastructure/
COPY src/Transactions/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj src/Transactions/CashFlow.Transactions.Api/
COPY src/Consolidation/CashFlow.Consolidation.Domain/CashFlow.Consolidation.Domain.csproj src/Consolidation/CashFlow.Consolidation.Domain/
COPY src/Consolidation/CashFlow.Consolidation.Application/CashFlow.Consolidation.Application.csproj src/Consolidation/CashFlow.Consolidation.Application/
COPY src/Consolidation/CashFlow.Consolidation.Infrastructure/CashFlow.Consolidation.Infrastructure.csproj src/Consolidation/CashFlow.Consolidation.Infrastructure/
COPY src/Consolidation/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj src/Consolidation/CashFlow.Consolidation.Api/
COPY tests/CashFlow.UnitTests/CashFlow.UnitTests.csproj tests/CashFlow.UnitTests/
COPY tests/CashFlow.IntegrationTests/CashFlow.IntegrationTests.csproj tests/CashFlow.IntegrationTests/

# Restore dependencies
RUN dotnet restore src/Transactions/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj

# Copy source code and publish
COPY src/Shared/ src/Shared/
COPY src/Transactions/ src/Transactions/

WORKDIR /app/src/Transactions/CashFlow.Transactions.Api
RUN dotnet publish -c Release -o /out --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CashFlow.Transactions.Api.dll"]
