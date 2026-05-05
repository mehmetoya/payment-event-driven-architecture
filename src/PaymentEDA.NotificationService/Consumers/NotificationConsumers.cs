using MassTransit;
using PaymentEDA.Contracts.Events;

namespace PaymentEDA.NotificationService.Consumers;

/// <summary>
/// Idempotent consumer for PaymentCreatedEvent.
/// Sends "Payment Initiated" notification to user.
/// </summary>
public class PaymentCreatedNotificationConsumer : IConsumer<PaymentCreatedEvent>
{
    private readonly INotificationSender _sender;
    private readonly ILogger<PaymentCreatedNotificationConsumer> _logger;

    public PaymentCreatedNotificationConsumer(INotificationSender sender, ILogger<PaymentCreatedNotificationConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCreatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Sending payment initiated notification for {PaymentId}", msg.PaymentId);
        await _sender.SendAsync(msg.UserId, "Payment Initiated",
            $"Your payment of {msg.Amount} {msg.Currency} has been initiated.");
    }
}

public class PaymentAuthorizedNotificationConsumer : IConsumer<PaymentAuthorizedEvent>
{
    private readonly INotificationSender _sender;
    private readonly ILogger<PaymentAuthorizedNotificationConsumer> _logger;

    public PaymentAuthorizedNotificationConsumer(INotificationSender sender, ILogger<PaymentAuthorizedNotificationConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentAuthorizedEvent> context)
    {
        _logger.LogInformation("Sending authorized notification for {PaymentId}", context.Message.PaymentId);
        await _sender.SendAsync(Guid.Empty, "Payment Authorized",
            $"Payment {context.Message.PaymentId} authorized. Code: {context.Message.AuthorizationCode}");
    }
}

public class PaymentCapturedNotificationConsumer : IConsumer<PaymentCapturedEvent>
{
    private readonly INotificationSender _sender;
    private readonly ILogger<PaymentCapturedNotificationConsumer> _logger;

    public PaymentCapturedNotificationConsumer(INotificationSender sender, ILogger<PaymentCapturedNotificationConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCapturedEvent> context)
    {
        _logger.LogInformation("Sending captured notification for {PaymentId}", context.Message.PaymentId);
        await _sender.SendAsync(Guid.Empty, "Payment Captured",
            $"Your payment of {context.Message.CapturedAmount} has been processed successfully.");
    }
}

public class PaymentFailedNotificationConsumer : IConsumer<PaymentFailedEvent>
{
    private readonly INotificationSender _sender;
    private readonly ILogger<PaymentFailedNotificationConsumer> _logger;

    public PaymentFailedNotificationConsumer(INotificationSender sender, ILogger<PaymentFailedNotificationConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        _logger.LogWarning("Sending failure notification for {PaymentId}", context.Message.PaymentId);
        await _sender.SendAsync(Guid.Empty, "Payment Failed",
            $"Your payment failed. Reason: {context.Message.Reason}");
    }
}

// ── Notification sender abstraction ──────────────────────────────────────────
public interface INotificationSender
{
    Task SendAsync(Guid userId, string subject, string body);
}

/// <summary>
/// Mock implementation. In production: SendGrid, FCM, SMS gateway, etc.
/// </summary>
public class LogNotificationSender : INotificationSender
{
    private readonly ILogger<LogNotificationSender> _logger;

    public LogNotificationSender(ILogger<LogNotificationSender> logger) => _logger = logger;

    public Task SendAsync(Guid userId, string subject, string body)
    {
        _logger.LogInformation("[NOTIFICATION] UserId={UserId} | {Subject}: {Body}", userId, subject, body);
        return Task.CompletedTask;
    }
}
