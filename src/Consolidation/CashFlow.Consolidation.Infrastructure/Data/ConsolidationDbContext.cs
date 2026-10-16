namespace CashFlow.Consolidation.Infrastructure.Data;

using CashFlow.Consolidation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public class ConsolidationDbContext : DbContext
{
    public ConsolidationDbContext(DbContextOptions<ConsolidationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DailyBalance>(builder =>
        {
            builder.HasKey(d => d.Date);
            builder.Property(d => d.Date).IsRequired();
            builder.Property(d => d.TotalCredits).HasPrecision(18, 2).IsRequired();
            builder.Property(d => d.TotalDebits).HasPrecision(18, 2).IsRequired();
            builder.Property(d => d.TransactionCount).IsRequired();
            builder.Property(d => d.UpdatedAt).IsRequired();
            builder.Ignore(d => d.NetBalance);
        });
    }
}
