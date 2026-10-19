namespace CashFlow.Transactions.Infrastructure.Data;

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
    }
}
