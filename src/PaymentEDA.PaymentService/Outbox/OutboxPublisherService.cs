using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentEDA.PaymentService.Data;

namespace PaymentEDA.PaymentService.Outbox;

/// <summary>
/// Background service that polls the OutboxMessages table and publishes
/// unprocessed events to RabbitMQ via MassTransit.
/// This is the "relay" part of the Outbox Pattern.
/// </summary>
public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public OutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Publisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        // Fetch unprocessed messages (max 50 at a time to avoid memory pressure)
        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (!messages.Any()) return;

        _logger.LogInformation("Processing {Count} outbox message(s).", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.EventType)
                    ?? throw new InvalidOperationException($"Unknown event type: {message.EventType}");

                var payload = JsonSerializer.Deserialize(message.Payload, eventType)
                    ?? throw new InvalidOperationException("Failed to deserialize payload.");

                await publishEndpoint.Publish(payload, eventType, ct);

                message.ProcessedAt = DateTime.UtcNow;
                _logger.LogInformation("Published outbox message {Id} ({Type})", message.Id, message.EventType);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                _logger.LogWarning(ex, "Failed to publish outbox message {Id}. Retry: {Retry}", message.Id, message.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
