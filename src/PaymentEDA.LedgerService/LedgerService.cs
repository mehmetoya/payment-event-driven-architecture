using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentEDA.Contracts.Events;

namespace PaymentEDA.LedgerService;

// ── Domain Model ──────────────────────────────────────────────────────────────
public class LedgerEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PaymentId { get; init; }
    public Guid MessageId { get; init; }  // Idempotency key
    public string EntryType { get; init; } = string.Empty;  // "CAPTURE", "SETTLEMENT"
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
}

// ── DbContext ─────────────────────────────────────────────────────────────────
public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<LedgerEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.HasIndex(x => x.MessageId).IsUnique();  // Idempotency
            e.HasIndex(x => x.PaymentId);
        });
    }
}

// ── Consumer ──────────────────────────────────────────────────────────────────
public class PaymentCapturedLedgerConsumer : IConsumer<PaymentCapturedEvent>
{
    private readonly LedgerDbContext _db;
    private readonly ILogger<PaymentCapturedLedgerConsumer> _logger;

    public PaymentCapturedLedgerConsumer(LedgerDbContext db, ILogger<PaymentCapturedLedgerConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCapturedEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        // Idempotency check
        if (await _db.LedgerEntries.AnyAsync(e => e.MessageId == messageId, context.CancellationToken))
        {
            _logger.LogWarning("Duplicate ledger entry for MessageId {MessageId}. Skipping.", messageId);
            return;
        }

        _db.LedgerEntries.Add(new LedgerEntry
        {
            PaymentId = msg.PaymentId,
            MessageId = messageId,
            EntryType = "CAPTURE",
            Amount = msg.CapturedAmount,
            Currency = "TRY",  // Would come from event in full impl
            Reference = msg.SettlementReference
        });

        await _db.SaveChangesAsync(context.CancellationToken);
        _logger.LogInformation("Ledger entry recorded for PaymentId {PaymentId}", msg.PaymentId);
    }
}
