using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using PaymentEDA.Contracts.Events;

// ── Domain ────────────────────────────────────────────────────────────────────
public class ReconciliationRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PaymentId { get; init; }
    public Guid MessageId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Reference { get; init; } = string.Empty;
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
}

public class ReconciliationDbContext : DbContext
{
    public ReconciliationDbContext(DbContextOptions<ReconciliationDbContext> o) : base(o) { }
    public DbSet<ReconciliationRecord> Records => Set<ReconciliationRecord>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<ReconciliationRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.HasIndex(x => x.MessageId).IsUnique();
        });
    }
}

// ── Consumer ──────────────────────────────────────────────────────────────────
public class PaymentSettledReconciliationConsumer : IConsumer<PaymentSettledEvent>
{
    private readonly ReconciliationDbContext _db;
    private readonly ILogger<PaymentSettledReconciliationConsumer> _logger;

    public PaymentSettledReconciliationConsumer(ReconciliationDbContext db, ILogger<PaymentSettledReconciliationConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentSettledEvent> context)
    {
        var messageId = context.MessageId ?? Guid.NewGuid();
        if (await _db.Records.AnyAsync(r => r.MessageId == messageId, context.CancellationToken))
        {
            _logger.LogWarning("Duplicate reconciliation for MessageId {MessageId}", messageId);
            return;
        }

        _db.Records.Add(new ReconciliationRecord
        {
            PaymentId = context.Message.PaymentId,
            MessageId = messageId,
            Status = "SETTLED",
            Amount = 0, // Would be enriched from payment service in prod
            Reference = $"SETTLE-{context.Message.PaymentId}"
        });

        await _db.SaveChangesAsync(context.CancellationToken);
        _logger.LogInformation("Reconciliation recorded for PaymentId {PaymentId}", context.Message.PaymentId);
    }
}

// ── Program ───────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<ReconciliationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("ReconciliationDb"),
        n => n.EnableRetryOnFailure(3)));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentSettledReconciliationConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });
        cfg.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
        cfg.UseDeadLetterQueueDeadLetterTransport();
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>().Database.MigrateAsync();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ReconciliationService" }));
app.Run();
