using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// 1. Infraestrutura: Suporte resiliente a Containers (Docker/Podman) ou Connection Strings existentes
var existingPostgres = builder.Configuration.GetConnectionString("transactionsdb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration.GetConnectionString("postgres");
var existingRabbit = builder.Configuration.GetConnectionString("rabbitmq")
    ?? builder.Configuration.GetConnectionString("RabbitMq");
var existingRedis = builder.Configuration.GetConnectionString("redis")
    ?? builder.Configuration.GetConnectionString("Redis");

IResourceBuilder<IResourceWithConnectionString> transactionsDb;
IResourceBuilder<IResourceWithConnectionString> consolidationDb;
IResourceBuilder<IResourceWithConnectionString> rabbitmq;
IResourceBuilder<IResourceWithConnectionString> redis;

if (!string.IsNullOrEmpty(existingPostgres))
{
    transactionsDb = builder.AddConnectionString("transactionsdb");
    consolidationDb = builder.AddConnectionString("consolidationdb");
}
else
{
    var postgres = builder.AddPostgres("postgres")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume();

    transactionsDb = postgres.AddDatabase("transactionsdb", "cashflow_transactions");
    consolidationDb = postgres.AddDatabase("consolidationdb", "cashflow_consolidation");
}

if (!string.IsNullOrEmpty(existingRabbit))
{
    rabbitmq = builder.AddConnectionString("rabbitmq");
}
else
{
    rabbitmq = builder.AddRabbitMQ("rabbitmq")
        .WithManagementPlugin()
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume();
}

if (!string.IsNullOrEmpty(existingRedis))
{
    redis = builder.AddConnectionString("redis");
}
else
{
    redis = builder.AddRedis("redis")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume();
}

// 2. Microsserviço de Transações (executado como processo .NET nativo)
var transactionsApi = builder.AddProject<Projects.CashFlow_Transactions_Api>("transactions-api")
    .WithReference(transactionsDb)
    .WithReference(rabbitmq);

if (string.IsNullOrEmpty(existingPostgres))
{
    transactionsApi.WaitFor(transactionsDb);
}
if (string.IsNullOrEmpty(existingRabbit))
{
    transactionsApi.WaitFor(rabbitmq);
}

// 3. Microsserviço de Consolidação de Saldo Diário (executado como processo .NET nativo)
var consolidationApi = builder.AddProject<Projects.CashFlow_Consolidation_Api>("consolidation-api")
    .WithReference(consolidationDb)
    .WithReference(rabbitmq)
    .WithReference(redis);

if (string.IsNullOrEmpty(existingPostgres))
{
    consolidationApi.WaitFor(consolidationDb);
}
if (string.IsNullOrEmpty(existingRabbit))
{
    consolidationApi.WaitFor(rabbitmq);
}
if (string.IsNullOrEmpty(existingRedis))
{
    consolidationApi.WaitFor(redis);
}

builder.Build().Run();

