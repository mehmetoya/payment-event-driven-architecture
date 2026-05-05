using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentEDA.Contracts.Events;
using PaymentEDA.FraudService.Data;
using PaymentEDA.FraudService.Services;

namespace PaymentEDA.FraudService.Consumers;

/// <summary>
/// Consumes PaymentCreatedEvent.
///
/// Implements TWO patterns:
/// 1. Idempotent Consumer — checks MessageId before processing.
///    If already processed, skips silently. Safe for at-least-once delivery.
///
/// 2. Dead Letter Queue (DLQ) — MassTransit automatically moves failed messages
///    to the "_error" queue after exhausting retries.
///    We configure this in Program.cs via UseMessageRetry + UseDeadLetterQueue.
/// </summary>
public class PaymentCreatedConsumer : IConsumer<PaymentCreatedEvent>
{
    private readonly FraudDbContext _db;
    private readonly IFraudAnalysisService _fraudService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PaymentCreatedConsumer> _logger;

    public PaymentCreatedConsumer(
        FraudDbContext db,
        IFraudAnalysisService fraudService,
        IPublishEndpoint publishEndpoint,
        ILogger<PaymentCreatedConsumer> logger)
    {
        _db = db;
        _fraudService = fraudService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCreatedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        // ── Idempotency Check ──────────────────────────────────────────────
        // If this MessageId was already processed, skip.
        // This handles RabbitMQ re-deliveries safely.
        var alreadyProcessed = await _db.FraudCheckResults
            .AnyAsync(f => f.MessageId == messageId, context.CancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogWarning(
                "Duplicate message detected. MessageId: {MessageId}, PaymentId: {PaymentId}. Skipping.",
                messageId, message.PaymentId);
            return;
        }

        _logger.LogInformation(
            "Processing fraud check for PaymentId: {PaymentId}", message.PaymentId);

        // ── Fraud Analysis ─────────────────────────────────────────────────
        var (isSuspicious, riskScore, reason) = await _fraudService.AnalyzeAsync(
            message.UserId, message.Amount, message.Currency, context.CancellationToken);

        // ── Persist Result (idempotent) ────────────────────────────────────
        var result = new FraudCheckResult
        {
            PaymentId = message.PaymentId,
            MessageId = messageId,
            IsSuspicious = isSuspicious,
            RiskScore = riskScore,
            Reason = reason
        };

        _db.FraudCheckResults.Add(result);
        await _db.SaveChangesAsync(context.CancellationToken);

        // ── Publish Result to Saga ─────────────────────────────────────────
        await _publishEndpoint.Publish(new FraudCheckCompleted
        {
            PaymentId = message.PaymentId,
            CorrelationId = message.CorrelationId,
            IsSuspicious = isSuspicious,
            RiskScore = riskScore,
            Reason = reason
        }, context.CancellationToken);

        _logger.LogInformation(
            "Fraud check completed for PaymentId: {PaymentId}. Suspicious: {IsSuspicious}, Score: {Score}",
            message.PaymentId, isSuspicious, riskScore);
    }
}

/// <summary>
/// Consumer definition — configures retry + DLQ for this consumer specifically.
/// MassTransit will send to "{queueName}_error" after retries exhausted.
/// </summary>
public class PaymentCreatedConsumerDefinition : ConsumerDefinition<PaymentCreatedConsumer>
{
    public PaymentCreatedConsumerDefinition()
    {
        // Queue name in RabbitMQ
        EndpointName = "fraud-service-payment-created";
        ConcurrentMessageLimit = 10;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PaymentCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Retry: 3 attempts with exponential backoff
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));

        // Dead Letter Queue: after retries, move to "fraud-service-payment-created_error"
        endpointConfigurator.UseDeadLetterQueueDeadLetterTransport();
        endpointConfigurator.UseDeadLetterQueueErrorTransport();

        // Schedule requeue for retryable messages after 1 minute
        endpointConfigurator.UseScheduledRedelivery(r =>
            r.Intervals(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)));
    }
}
