namespace PaymentEDA.PaymentService.Outbox;

/// <summary>
/// Outbox Pattern: Events are first persisted to this table
/// in the same DB transaction as the domain change.
/// A background worker then publishes them to RabbitMQ.
/// This guarantees at-least-once delivery and prevents dual-write issues.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Full type name of the event (e.g., PaymentEDA.Contracts.Events.PaymentCreatedEvent)</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>JSON-serialized event payload</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>RabbitMQ routing / exchange name</summary>
    public string ExchangeName { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Null = not yet published</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Retry counter for failed publishes</summary>
    public int RetryCount { get; set; }

    public string? Error { get; set; }
}
