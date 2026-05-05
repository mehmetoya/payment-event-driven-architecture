using Microsoft.EntityFrameworkCore;

namespace PaymentEDA.FraudService.Data;

public class FraudDbContext : DbContext
{
    public FraudDbContext(DbContextOptions<FraudDbContext> options) : base(options) { }

    public DbSet<FraudCheckResult> FraudCheckResults => Set<FraudCheckResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FraudCheckResult>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.RiskScore).HasPrecision(5, 2);

            // Unique index on MessageId → guarantees idempotency at DB level
            entity.HasIndex(f => f.MessageId).IsUnique();
            entity.HasIndex(f => f.PaymentId);
        });
    }
}
