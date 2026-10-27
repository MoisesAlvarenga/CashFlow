namespace CashFlow.Consolidation.Infrastructure.Data;

using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Shared.Contracts.Security;
using Microsoft.EntityFrameworkCore;

public class ConsolidationDbContext : DbContext
{
    public ConsolidationDbContext(DbContextOptions<ConsolidationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();
    public DbSet<ConsumedMessage> ConsumedMessages => Set<ConsumedMessage>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DailyBalance>(builder =>
        {
            builder.ToTable("DailyBalances");
            builder.HasKey(d => d.Date);
            builder.Property(d => d.Date).IsRequired();
            builder.Property(d => d.TotalCredits).HasPrecision(18, 2).IsRequired();
            builder.Property(d => d.TotalDebits).HasPrecision(18, 2).IsRequired();
            builder.Property(d => d.TransactionCount).IsRequired();
            builder.Property(d => d.UpdatedAt).IsRequired();
            builder.Ignore(d => d.NetBalance);
        });

        modelBuilder.Entity<ConsumedMessage>(builder =>
        {
            builder.ToTable("ConsumedMessages");
            builder.HasKey(message => message.TransactionId).HasName("PK_ConsumedMessages");
            builder.Property(message => message.TransactionId).IsRequired();
            builder.Property(message => message.EventType).HasMaxLength(150).IsRequired();
            builder.Property(message => message.ConsumedAt).IsRequired();
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
