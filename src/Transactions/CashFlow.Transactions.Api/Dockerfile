# Multi-stage Dockerfile for CashFlow.Transactions.Api (.NET 9)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for optimized layer caching
COPY src/Shared/CashFlow.Shared.Contracts/CashFlow.Shared.Contracts.csproj src/Shared/CashFlow.Shared.Contracts/
COPY src/Transactions/CashFlow.Transactions.Domain/CashFlow.Transactions.Domain.csproj src/Transactions/CashFlow.Transactions.Domain/
COPY src/Transactions/CashFlow.Transactions.Application/CashFlow.Transactions.Application.csproj src/Transactions/CashFlow.Transactions.Application/
COPY src/Transactions/CashFlow.Transactions.Infrastructure/CashFlow.Transactions.Infrastructure.csproj src/Transactions/CashFlow.Transactions.Infrastructure/
COPY src/Transactions/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj src/Transactions/CashFlow.Transactions.Api/

# Restore dependencies
RUN dotnet restore src/Transactions/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj

# Copy source code
COPY src/Shared/ src/Shared/
COPY src/Transactions/ src/Transactions/

# Publish
WORKDIR /src/src/Transactions/CashFlow.Transactions.Api
RUN dotnet publish CashFlow.Transactions.Api.csproj -c Release -o /app/publish

# Runtime image (.NET 9 ASP.NET non-root)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "CashFlow.Transactions.Api.dll"]
