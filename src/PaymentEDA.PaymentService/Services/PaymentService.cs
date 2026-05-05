using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentEDA.Contracts.Enums;
using PaymentEDA.Contracts.Events;
using PaymentEDA.PaymentService.Data;
using PaymentEDA.PaymentService.Models;
using PaymentEDA.PaymentService.Outbox;

namespace PaymentEDA.PaymentService.Services;

public interface IPaymentService
{
    Task<Payment> CreatePaymentAsync(
        Guid merchantId, Guid userId,
        decimal amount, Currency currency,
        PaymentMethod method, string? description,
        CancellationToken ct = default);

    Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken ct = default);
    Task<IEnumerable<Payment>> GetMerchantPaymentsAsync(Guid merchantId, CancellationToken ct = default);
}

public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _db;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(PaymentDbContext db, ILogger<PaymentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Payment> CreatePaymentAsync(
        Guid merchantId, Guid userId,
        decimal amount, Currency currency,
        PaymentMethod method, string? description,
        CancellationToken ct = default)
    {
        // 1. Create domain object
        var payment = Payment.Create(merchantId, userId, amount, currency, method, description);

        // 2. Create outbox message (Outbox Pattern)
        //    Both happen in ONE transaction → no dual-write problem
        var outboxMessage = new OutboxMessage
        {
            EventType = typeof(PaymentCreatedEvent).AssemblyQualifiedName!,
            ExchangeName = "payment-created",
            Payload = JsonSerializer.Serialize(new PaymentCreatedEvent
            {
                PaymentId = payment.Id,
                CorrelationId = payment.CorrelationId,
                MerchantId = payment.MerchantId,
                UserId = payment.UserId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Method = payment.Method,
                Description = payment.Description,
                OccurredAt = DateTime.UtcNow
            })
        };

        // 3. Persist atomically ✅
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Payments.Add(payment);
            _db.OutboxMessages.Add(outboxMessage);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Payment {PaymentId} created. Outbox message {OutboxId} queued.",
                payment.Id, outboxMessage.Id);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return payment;
    }

    public Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken ct = default)
        => _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public Task<IEnumerable<Payment>> GetMerchantPaymentsAsync(Guid merchantId, CancellationToken ct = default)
        => _db.Payments
            .Where(p => p.MerchantId == merchantId)
            .OrderByDescending(p => p.CreatedAt)
            .AsAsyncEnumerable()
            .ToListAsync(ct)
            .ContinueWith(t => t.Result.AsEnumerable(), ct);
}

// ── Extension helpers ──────────────────────────────────────────────────────
file static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(ct))
            list.Add(item);
        return list;
    }
}
