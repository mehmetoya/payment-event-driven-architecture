using Microsoft.AspNetCore.Mvc;
using PaymentEDA.Contracts.Enums;
using PaymentEDA.PaymentService.Services;

namespace PaymentEDA.PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>Create a new payment and queue it for processing.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatePaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        CancellationToken ct)
    {
        if (request.Amount <= 0)
            return BadRequest("Amount must be positive.");

        var payment = await _paymentService.CreatePaymentAsync(
            request.MerchantId,
            request.UserId,
            request.Amount,
            request.Currency,
            request.Method,
            request.Description,
            ct);

        _logger.LogInformation("Payment {PaymentId} created via API.", payment.Id);

        var response = new CreatePaymentResponse(
            payment.Id,
            payment.CorrelationId,
            payment.Status.ToString(),
            payment.Amount,
            payment.Currency.ToString());

        return CreatedAtAction(nameof(GetPayment), new { paymentId = payment.Id }, response);
    }

    /// <summary>Get payment by ID.</summary>
    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(Guid paymentId, CancellationToken ct)
    {
        var payment = await _paymentService.GetPaymentAsync(paymentId, ct);
        if (payment is null) return NotFound();

        return Ok(new PaymentDto(
            payment.Id,
            payment.Status.ToString(),
            payment.Amount,
            payment.Currency.ToString(),
            payment.CreatedAt));
    }

    /// <summary>Get all payments for a merchant.</summary>
    [HttpGet("merchant/{merchantId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMerchantPayments(Guid merchantId, CancellationToken ct)
    {
        var payments = await _paymentService.GetMerchantPaymentsAsync(merchantId, ct);
        var dtos = payments.Select(p => new PaymentDto(
            p.Id, p.Status.ToString(), p.Amount, p.Currency.ToString(), p.CreatedAt));
        return Ok(dtos);
    }
}

// ── Request / Response DTOs ────────────────────────────────────────────────
public record CreatePaymentRequest(
    Guid MerchantId,
    Guid UserId,
    decimal Amount,
    Currency Currency,
    PaymentMethod Method,
    string? Description);

public record CreatePaymentResponse(
    Guid PaymentId,
    Guid CorrelationId,
    string Status,
    decimal Amount,
    string Currency);

public record PaymentDto(
    Guid PaymentId,
    string Status,
    decimal Amount,
    string Currency,
    DateTime CreatedAt);
