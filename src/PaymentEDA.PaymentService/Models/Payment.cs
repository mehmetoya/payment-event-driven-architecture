using PaymentEDA.Contracts.Enums;

namespace PaymentEDA.PaymentService.Models;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid CorrelationId { get; private set; }  // Saga correlation
    public Guid MerchantId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public Currency Currency { get; private set; }
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? Description { get; private set; }
    public string? AuthorizationCode { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core constructor
    private Payment() { }

    public static Payment Create(
        Guid merchantId,
        Guid userId,
        decimal amount,
        Currency currency,
        PaymentMethod method,
        string? description = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        return new Payment
        {
            Id = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            MerchantId = merchantId,
            UserId = userId,
            Amount = amount,
            Currency = currency,
            Method = method,
            Status = PaymentStatus.Initiated,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Authorize(string authorizationCode)
    {
        if (Status != PaymentStatus.Initiated)
            throw new InvalidOperationException($"Cannot authorize payment in status: {Status}");

        AuthorizationCode = authorizationCode;
        Status = PaymentStatus.Authorized;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Capture()
    {
        if (Status != PaymentStatus.Authorized)
            throw new InvalidOperationException($"Cannot capture payment in status: {Status}");

        Status = PaymentStatus.Captured;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        if (Status is PaymentStatus.Settled or PaymentStatus.Refunded)
            throw new InvalidOperationException($"Cannot fail payment in status: {Status}");

        FailureReason = reason;
        Status = PaymentStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Settle()
    {
        if (Status != PaymentStatus.Captured)
            throw new InvalidOperationException($"Cannot settle payment in status: {Status}");

        Status = PaymentStatus.Settled;
        UpdatedAt = DateTime.UtcNow;
    }
}
