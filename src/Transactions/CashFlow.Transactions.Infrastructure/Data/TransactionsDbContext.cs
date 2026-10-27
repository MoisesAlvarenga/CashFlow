namespace CashFlow.Transactions.Infrastructure.Data;

using CashFlow.Shared.Contracts.Security;
using CashFlow.Transactions.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public class TransactionsDbContext : DbContext
{
    public TransactionsDbContext(DbContextOptions<TransactionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(builder =>
        {
            builder.ToTable("Transactions");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(t => t.Description).HasMaxLength(255).IsRequired();
            builder.Property(t => t.Type).IsRequired();
            builder.Property(t => t.Date).IsRequired();
            builder.Property(t => t.CreatedAt).IsRequired();
            builder.HasIndex(t => t.Date).HasDatabaseName("IX_Transactions_Date");
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.EventType).HasMaxLength(150).IsRequired();
            builder.Property(o => o.Payload).IsRequired();
            builder.Property(o => o.CreatedAt).IsRequired();
            builder.Property(o => o.ProcessedAt).IsRequired(false);
            builder.Property(o => o.ErrorMessage).HasMaxLength(1000).IsRequired(false);
            builder.Property(o => o.RetryCount).IsRequired();
            builder.HasIndex(o => o.ProcessedAt).HasDatabaseName("IX_OutboxMessages_ProcessedAt");
        });

        modelBuilder.Entity<IdempotencyRecord>(builder =>
        {
            builder.ToTable("IdempotencyRecords");
            builder.HasKey(r => r.Key).HasName("PK_IdempotencyRecords");
            builder.Property(r => r.Key).HasMaxLength(128).IsRequired();
            builder.Property(r => r.TransactionId).IsRequired();
            builder.Property(r => r.ResponsePayload).IsRequired();
            builder.Property(r => r.StatusCode).IsRequired();
            builder.Property(r => r.CreatedAt).IsRequired();
            builder.HasIndex(r => r.CreatedAt).HasDatabaseName("IX_IdempotencyRecords_CreatedAt");
        });

        modelBuilder.Entity<ApiClient>(builder =>
        {
            builder.ToTable("ApiClients");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.ClientId).HasMaxLength(100).IsRequired();
            builder.HasIndex(c => c.ClientId).IsUnique().HasDatabaseName("IX_ApiClients_ClientId");
            builder.Property(c => c.SecretHash).HasMaxLength(256).IsRequired();
            builder.Property(c => c.Salt).HasMaxLength(128).IsRequired();
            builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
            builder.Property(c => c.Roles).HasMaxLength(100).IsRequired();
            builder.Property(c => c.IsActive).IsRequired();
            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.UpdatedAt).IsRequired(false);
        });
    }
}
