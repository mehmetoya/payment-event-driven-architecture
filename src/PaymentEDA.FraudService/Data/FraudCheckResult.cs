namespace PaymentEDA.FraudService.Data;

public class FraudCheckResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PaymentId { get; init; }

    /// <summary>
    /// Idempotency key — MessageId from MassTransit.
    /// We check this before processing to skip duplicate messages.
    /// </summary>
    public Guid MessageId { get; init; }

    public bool IsSuspicious { get; init; }
    public decimal RiskScore { get; init; }
    public string? Reason { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}
