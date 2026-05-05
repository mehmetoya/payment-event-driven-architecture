using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using PaymentEDA.Contracts.Events;

// ── Domain ────────────────────────────────────────────────────────────────────
public class PaymentAnalyticsEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PaymentId { get; init; }
    public Guid MessageId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public DateTime OccurredAt { get; init; }
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
}

public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> o) : base(o) { }
    public DbSet<PaymentAnalyticsEvent> Events => Set<PaymentAnalyticsEvent>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<PaymentAnalyticsEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.HasIndex(x => x.MessageId).IsUnique(); // Idempotency
            e.HasIndex(x => new { x.EventType, x.OccurredAt });
        });
    }
}

// ── Generic idempotent analytics consumer ────────────────────────────────────
public class AnalyticsConsumer :
    IConsumer<PaymentCreatedEvent>,
    IConsumer<PaymentAuthorizedEvent>,
    IConsumer<PaymentCapturedEvent>,
    IConsumer<PaymentFailedEvent>,
    IConsumer<PaymentSettledEvent>
{
    private readonly AnalyticsDbContext _db;
    private readonly ILogger<AnalyticsConsumer> _logger;

    public AnalyticsConsumer(AnalyticsDbContext db, ILogger<AnalyticsConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PaymentCreatedEvent> ctx)
        => RecordAsync(ctx.MessageId ?? Guid.NewGuid(), ctx.Message.PaymentId,
            "PaymentCreated", ctx.Message.Amount, ctx.Message.Currency.ToString(), ctx.Message.OccurredAt, ctx.CancellationToken);

    public Task Consume(ConsumeContext<PaymentAuthorizedEvent> ctx)
        => RecordAsync(ctx.MessageId ?? Guid.NewGuid(), ctx.Message.PaymentId,
            "PaymentAuthorized", null, null, ctx.Message.OccurredAt, ctx.CancellationToken);

    public Task Consume(ConsumeContext<PaymentCapturedEvent> ctx)
        => RecordAsync(ctx.MessageId ?? Guid.NewGuid(), ctx.Message.PaymentId,
            "PaymentCaptured", ctx.Message.CapturedAmount, null, ctx.Message.OccurredAt, ctx.CancellationToken);

    public Task Consume(ConsumeContext<PaymentFailedEvent> ctx)
        => RecordAsync(ctx.MessageId ?? Guid.NewGuid(), ctx.Message.PaymentId,
            "PaymentFailed", null, null, ctx.Message.OccurredAt, ctx.CancellationToken);

    public Task Consume(ConsumeContext<PaymentSettledEvent> ctx)
        => RecordAsync(ctx.MessageId ?? Guid.NewGuid(), ctx.Message.PaymentId,
            "PaymentSettled", null, null, ctx.Message.OccurredAt, ctx.CancellationToken);

    private async Task RecordAsync(Guid messageId, Guid paymentId, string eventType,
        decimal? amount, string? currency, DateTime occurredAt, CancellationToken ct)
    {
        if (await _db.Events.AnyAsync(e => e.MessageId == messageId, ct))
        {
            _logger.LogWarning("Duplicate analytics event for MessageId {MessageId}", messageId);
            return;
        }

        _db.Events.Add(new PaymentAnalyticsEvent
        {
            PaymentId = paymentId,
            MessageId = messageId,
            EventType = eventType,
            Amount = amount,
            Currency = currency,
            OccurredAt = occurredAt
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Analytics recorded: {EventType} for {PaymentId}", eventType, paymentId);
    }
}

// ── Program ───────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<AnalyticsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("AnalyticsDb"),
        n => n.EnableRetryOnFailure(3)));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AnalyticsConsumer>();
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
    await scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().Database.MigrateAsync();

// Simple analytics query endpoint
app.MapGet("/analytics/summary", async (AnalyticsDbContext db) =>
{
    var summary = await db.Events
        .GroupBy(e => e.EventType)
        .Select(g => new { EventType = g.Key, Count = g.Count() })
        .ToListAsync();
    return Results.Ok(summary);
});

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "AnalyticsService" }));
app.Run();
