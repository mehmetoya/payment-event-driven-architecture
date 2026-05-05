using Microsoft.EntityFrameworkCore;
using PaymentEDA.PaymentService.Models;
using PaymentEDA.PaymentService.Outbox;

namespace PaymentEDA.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Payment ────────────────────────────────────────────────────────
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasPrecision(18, 4);
            entity.Property(p => p.Status).HasConversion<string>();
            entity.Property(p => p.Currency).HasConversion<string>();
            entity.Property(p => p.Method).HasConversion<string>();
            entity.HasIndex(p => p.CorrelationId).IsUnique();
            entity.HasIndex(p => new { p.MerchantId, p.Status });
        });

        // ── OutboxMessage ─────────────────────────────────────────────────
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => o.ProcessedAt);  // Fast polling query
            entity.HasIndex(o => o.CreatedAt);
        });
    }
}
