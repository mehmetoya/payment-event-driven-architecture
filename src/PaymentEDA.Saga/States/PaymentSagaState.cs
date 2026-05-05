using MassTransit;

namespace PaymentEDA.Saga.States;

/// <summary>
/// Saga state — persisted to DB by MassTransit.
/// Tracks the current state of a payment flow across multiple events.
/// CorrelationId ties all events together.
/// </summary>
public class PaymentSagaState : SagaStateMachineInstance
{
    /// <summary>Must match CorrelationId in all events.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Current state name (e.g., "Created", "FraudChecked", "Authorized")</summary>
    public string CurrentState { get; set; } = string.Empty;

    public Guid PaymentId { get; set; }
    public Guid MerchantId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }

    // ── Fraud ──────────────────────────────────────────────────────────────
    public bool? FraudCheckPassed { get; set; }
    public decimal? FraudRiskScore { get; set; }

    // ── Authorization ──────────────────────────────────────────────────────
    public string? AuthorizationCode { get; set; }

    // ── Timing ────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }

    // ── Failure info ──────────────────────────────────────────────────────
    public string? FailureReason { get; set; }
}
