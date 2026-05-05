using PaymentEDA.Contracts.Enums;

namespace PaymentEDA.FraudService.Services;

public interface IFraudAnalysisService
{
    Task<(bool IsSuspicious, decimal RiskScore, string? Reason)> AnalyzeAsync(
        Guid userId, decimal amount, Currency currency, CancellationToken ct = default);
}

/// <summary>
/// Simulates a fraud scoring engine.
/// In production, this would call an ML model or third-party fraud API.
/// </summary>
public class FraudAnalysisService : IFraudAnalysisService
{
    private readonly ILogger<FraudAnalysisService> _logger;

    public FraudAnalysisService(ILogger<FraudAnalysisService> logger)
    {
        _logger = logger;
    }

    public async Task<(bool IsSuspicious, decimal RiskScore, string? Reason)> AnalyzeAsync(
        Guid userId, decimal amount, Currency currency, CancellationToken ct = default)
    {
        // Simulate async analysis (e.g., calling ML API)
        await Task.Delay(50, ct);

        decimal riskScore = 0;
        string? reason = null;

        // Rule 1: Very high amount
        if (amount > 50_000)
        {
            riskScore += 40;
            reason = "High-value transaction";
        }

        // Rule 2: Round amounts often indicate automated/fraudulent activity
        if (amount % 1000 == 0 && amount > 5000)
        {
            riskScore += 20;
            reason = reason is null ? "Suspicious round amount" : $"{reason}; Round amount";
        }

        // Rule 3: Simulate velocity check (would query DB in real impl)
        var randomVelocityRisk = new Random().Next(0, 15);
        riskScore += randomVelocityRisk;

        var isSuspicious = riskScore >= 60;

        _logger.LogDebug(
            "Fraud analysis: UserId={UserId}, Amount={Amount}, Score={Score}, Suspicious={IsSuspicious}",
            userId, amount, riskScore, isSuspicious);

        return (isSuspicious, Math.Min(riskScore, 100), reason);
    }
}
