var builder = DistributedApplication.CreateBuilder(args);

// Infraestrutura base (PostgreSQL com banco para transações e consolidação)
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var transactionsDb = postgres.AddDatabase("transactionsdb", "cashflow_transactions");
var consolidationDb = postgres.AddDatabase("consolidationdb", "cashflow_consolidation");

// Mensageria assíncrona (RabbitMQ com plugin de gerenciamento)
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithDataVolume();

// Cache distribuído (Redis para consolidação de saldos diários - EVO-02)
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// Microsserviço de Transações
var transactionsApi = builder.AddProject<Projects.CashFlow_Transactions_Api>("transactions-api")
    .WithReference(transactionsDb)
    .WithReference(rabbitmq)
    .WaitFor(transactionsDb)
    .WaitFor(rabbitmq);

// Microsserviço de Consolidação de Saldo Diário
var consolidationApi = builder.AddProject<Projects.CashFlow_Consolidation_Api>("consolidation-api")
    .WithReference(consolidationDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(consolidationDb)
    .WaitFor(rabbitmq)
    .WaitFor(redis);

builder.Build().Run();
