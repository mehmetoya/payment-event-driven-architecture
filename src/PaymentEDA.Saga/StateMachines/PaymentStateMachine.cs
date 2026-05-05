using MassTransit;
using PaymentEDA.Contracts.Events;
using PaymentEDA.Saga.States;

namespace PaymentEDA.Saga.StateMachines;

/// <summary>
/// Payment Saga — orchestrates the full payment lifecycle.
///
/// State transitions:
///   Initial
///     → [PaymentCreatedEvent]     → Created
///   Created
///     → [FraudCheckCompleted] (pass)  → FraudCleared
///     → [FraudCheckCompleted] (fail)  → Failed
///   FraudCleared
///     → [PaymentAuthorizedEvent]  → Authorized
///     → [PaymentFailedEvent]      → Failed
///   Authorized
///     → [PaymentCapturedEvent]    → Captured
///     → [PaymentFailedEvent]      → Failed
///   Captured
///     → [PaymentSettledEvent]     → Final (Settled)
///   Any
///     → [PaymentFailedEvent]      → Failed
/// </summary>
public class PaymentStateMachine : MassTransitStateMachine<PaymentSagaState>
{
    // ── States ─────────────────────────────────────────────────────────────
    public State Created { get; private set; } = null!;
    public State FraudCleared { get; private set; } = null!;
    public State Authorized { get; private set; } = null!;
    public State Captured { get; private set; } = null!;
    public State Settled { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    // ── Events ─────────────────────────────────────────────────────────────
    public Event<PaymentCreatedEvent> PaymentCreated { get; private set; } = null!;
    public Event<FraudCheckCompleted> FraudCheckCompleted { get; private set; } = null!;
    public Event<PaymentAuthorizedEvent> PaymentAuthorized { get; private set; } = null!;
    public Event<PaymentCapturedEvent> PaymentCaptured { get; private set; } = null!;
    public Event<PaymentSettledEvent> PaymentSettled { get; private set; } = null!;
    public Event<PaymentFailedEvent> PaymentFailed { get; private set; } = null!;

    public PaymentStateMachine()
    {
        // CorrelationId binding: how each event maps to a saga instance
        Event(() => PaymentCreated, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => FraudCheckCompleted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentAuthorized, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentCaptured, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentSettled, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentFailed, x => x.CorrelateById(m => m.Message.CorrelationId));

        // Initial state stored as the "CurrentState" string property
        InstanceState(x => x.CurrentState);

        // ── Transitions ────────────────────────────────────────────────────

        Initially(
            When(PaymentCreated)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentId = ctx.Message.PaymentId;
                    ctx.Saga.MerchantId = ctx.Message.MerchantId;
                    ctx.Saga.UserId = ctx.Message.UserId;
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.CreatedAt = ctx.Message.OccurredAt;
                })
                .Publish(ctx => new FraudCheckRequested
                {
                    PaymentId = ctx.Saga.PaymentId,
                    CorrelationId = ctx.Saga.CorrelationId,
                    UserId = ctx.Saga.UserId,
                    Amount = ctx.Saga.Amount,
                    Currency = ctx.Message.Currency
                })
                .TransitionTo(Created));

        During(Created,
            When(FraudCheckCompleted, ctx => !ctx.Message.IsSuspicious)
                .Then(ctx =>
                {
                    ctx.Saga.FraudCheckPassed = true;
                    ctx.Saga.FraudRiskScore = ctx.Message.RiskScore;
                })
                .TransitionTo(FraudCleared),

            When(FraudCheckCompleted, ctx => ctx.Message.IsSuspicious)
                .Then(ctx =>
                {
                    ctx.Saga.FraudCheckPassed = false;
                    ctx.Saga.FraudRiskScore = ctx.Message.RiskScore;
                    ctx.Saga.FailureReason = $"Fraud detected: {ctx.Message.Reason}";
                    ctx.Saga.FailedAt = DateTime.UtcNow;
                })
                .Publish(ctx => new PaymentFailedEvent
                {
                    PaymentId = ctx.Saga.PaymentId,
                    CorrelationId = ctx.Saga.CorrelationId,
                    ErrorCode = "FRAUD_DETECTED",
                    Reason = ctx.Saga.FailureReason ?? "Fraud check failed",
                    IsRetryable = false
                })
                .TransitionTo(Failed));

        During(FraudCleared,
            When(PaymentAuthorized)
                .Then(ctx =>
                {
                    ctx.Saga.AuthorizationCode = ctx.Message.AuthorizationCode;
                })
                .TransitionTo(Authorized));

        During(Authorized,
            When(PaymentCaptured)
                .TransitionTo(Captured));

        During(Captured,
            When(PaymentSettled)
                .Then(ctx => ctx.Saga.CompletedAt = DateTime.UtcNow)
                .TransitionTo(Settled)
                .Finalize());

        // PaymentFailed can happen at any active state
        DuringAny(
            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    ctx.Saga.FailedAt = DateTime.UtcNow;
                })
                .TransitionTo(Failed));

        // Remove finalized sagas from storage (optional — comment out to keep history)
        SetCompletedWhenFinalized();
    }
}
