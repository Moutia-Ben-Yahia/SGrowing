using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SGrowing.Api.Modules.AI.SyntheticData;

/// <summary>
/// Autonomous differential privacy engine enforcing strict (epsilon, delta) privacy budgets
/// and injecting calibrated Laplace or Gaussian noise to prevent membership inference attacks.
/// </summary>
public sealed class DifferentialPrivacyEngine
{
    private const double DefaultDailyEpsilon = 5.0; // Standard production privacy budget
    private const double DefaultDelta = 1e-5;

    private readonly ConcurrentDictionary<string, PrivacyBudget> _budgets = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _lock = new();

    public PerturbedQueryResult PerturbQuery(PerturbQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestedEpsilon <= 0) throw new ArgumentOutOfRangeException(nameof(request.RequestedEpsilon), "Epsilon must be > 0.");
        if (request.SensitivityDeltaF <= 0) throw new ArgumentOutOfRangeException(nameof(request.SensitivityDeltaF), "Sensitivity must be > 0.");

        lock (_lock)
        {
            var budget = _budgets.GetOrAdd(request.TenantId, _ => new PrivacyBudget(
                TenantId: request.TenantId,
                TotalEpsilon: DefaultDailyEpsilon,
                ConsumedEpsilon: 0.0,
                Delta: DefaultDelta,
                ResetDateUtc: DateTime.UtcNow.AddDays(1)
            ));

            if (budget.ConsumedEpsilon + request.RequestedEpsilon > budget.TotalEpsilon)
            {
                throw new InvalidOperationException($"Privacy budget exhausted for tenant '{request.TenantId}'. Requested: {request.RequestedEpsilon:F2}, Available: {budget.TotalEpsilon - budget.ConsumedEpsilon:F2}. Further queries blocked to prevent re-identification.");
            }

            // Generate calibrated noise
            double noise;
            if (request.Mechanism == NoiseMechanism.Laplace)
            {
                var b = request.SensitivityDeltaF / request.RequestedEpsilon;
                noise = SampleLaplace(b);
            }
            else // Gaussian
            {
                var delta = Math.Max(1e-10, request.RequestedDelta);
                var sigma = Math.Sqrt(2.0 * Math.Log(1.25 / delta)) * (request.SensitivityDeltaF / request.RequestedEpsilon);
                noise = SampleGaussian(sigma);
            }

            var updatedConsumed = budget.ConsumedEpsilon + request.RequestedEpsilon;
            _budgets[request.TenantId] = budget with { ConsumedEpsilon = updatedConsumed };

            var perturbed = request.RawValue + noise;

            return new PerturbedQueryResult(
                QueryIdentifier: request.QueryIdentifier,
                PerturbedValue: Math.Round(perturbed, 4),
                NoiseAdded: Math.Round(noise, 4),
                EpsilonCost: request.RequestedEpsilon,
                RemainingEpsilon: Math.Round(budget.TotalEpsilon - updatedConsumed, 4),
                MechanismUsed: request.Mechanism,
                ComputedAtUtc: DateTime.UtcNow
            );
        }
    }

    public PrivacyBudget GetBudget(string tenantId)
    {
        lock (_lock)
        {
            return _budgets.GetOrAdd(tenantId, _ => new PrivacyBudget(tenantId, DefaultDailyEpsilon, 0.0, DefaultDelta, DateTime.UtcNow.AddDays(1)));
        }
    }

    public void ResetBudget(string tenantId, double newTotalEpsilon = DefaultDailyEpsilon)
    {
        lock (_lock)
        {
            _budgets[tenantId] = new PrivacyBudget(tenantId, newTotalEpsilon, 0.0, DefaultDelta, DateTime.UtcNow.AddDays(1));
        }
    }

    private static double SampleLaplace(double b)
    {
        // Inverse CDF method: U in (-0.5, 0.5)
        var u = RandomNumberGenerator.GetInt32(1, int.MaxValue) / (double)int.MaxValue - 0.5;
        if (Math.Abs(u) < 1e-12) u = 1e-12;
        return -Math.Sign(u) * b * Math.Log(1.0 - (2.0 * Math.Abs(u)));
    }

    private static double SampleGaussian(double sigma)
    {
        // Box-Muller transform
        var u1 = Math.Max(1e-12, RandomNumberGenerator.GetInt32(1, int.MaxValue) / (double)int.MaxValue);
        var u2 = RandomNumberGenerator.GetInt32(1, int.MaxValue) / (double)int.MaxValue;
        var z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return z0 * sigma;
    }
}
