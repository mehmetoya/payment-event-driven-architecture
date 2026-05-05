using PaymentEDA.Contracts.Enums;

namespace PaymentEDA.Contracts.Events;

// ─── Base Interface ───────────────────────────────────────────────────────────
public interface IPaymentEvent
{
    Guid PaymentId { get; }
    Guid CorrelationId { get; }
    DateTime OccurredAt { get; }
}

// ─── PaymentCreated ──────────────────────────────────────────────────────────
public record PaymentCreatedEvent : IPaymentEvent
{
    public Guid PaymentId { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid MerchantId { get; init; }
    public Guid UserId { get; init; }
    public decimal Amount { get; init; }
    public Currency Currency { get; init; }
    public PaymentMethod Method { get; init; }
    public string? Description { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

// ─── PaymentAuthorized ───────────────────────────────────────────────────────
public record PaymentAuthorizedEvent : IPaymentEvent
{
    public Guid PaymentId { get; init; }
    public Guid CorrelationId { get; init; }
    public string AuthorizationCode { get; init; } = string.Empty;
    public string ProcessorResponse { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

// ─── PaymentCaptured ────────────────────────────────────────────────────────
public record PaymentCapturedEvent : IPaymentEvent
{
    public Guid PaymentId { get; init; }
    public Guid CorrelationId { get; init; }
    public decimal CapturedAmount { get; init; }
    public string SettlementReference { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

// ─── PaymentFailed ───────────────────────────────────────────────────────────
public record PaymentFailedEvent : IPaymentEvent
{
    public Guid PaymentId { get; init; }
    public Guid CorrelationId { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool IsRetryable { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

// ─── PaymentSettled ──────────────────────────────────────────────────────────
public record PaymentSettledEvent : IPaymentEvent
{
    public Guid PaymentId { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime SettledAt { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

// ─── FraudCheckRequested (Saga Command) ──────────────────────────────────────
public record FraudCheckRequested
{
    public Guid PaymentId { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid UserId { get; init; }
    public decimal Amount { get; init; }
    public Currency Currency { get; init; }
}

// ─── FraudCheckCompleted ────────────────────────────────────────────────────
public record FraudCheckCompleted
{
    public Guid PaymentId { get; init; }
    public Guid CorrelationId { get; init; }
    public bool IsSuspicious { get; init; }
    public string? Reason { get; init; }
    public decimal RiskScore { get; init; }
}
