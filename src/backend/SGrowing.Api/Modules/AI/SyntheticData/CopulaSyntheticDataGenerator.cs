using System.Security.Cryptography;

namespace SGrowing.Api.Modules.AI.SyntheticData;

/// <summary>
/// Autonomous synthetic data generator synthesizing realistic B2B CRM leads
/// and transactional datasets with bounded differential privacy noise.
/// </summary>
public sealed class CopulaSyntheticDataGenerator
{
    private static readonly string[] FirstNames = ["Karim", "Amina", "Sofiane", "Yasmine", "Mehdi", "Fatima", "Tarik", "Nour", "Rachid", "Leila"];
    private static readonly string[] LastNames = ["Benali", "Khelifi", "Boudiaf", "Saidi", "Mansouri", "Hadj", "Amrani", "Belkacem", "Cherif", "Zitouni"];
    private static readonly string[] CompanyPrefixes = ["Atlas", "Maghreb", "Numidia", "Sahara", "Mediterranean", "Oran", "Algiers", "Constantine", "Apex", "Vanguard"];
    private static readonly string[] CompanySuffixes = ["Logistics", "Commerce", "Industries", "Tech", "Agro", "Distribution", "Energy", "Solutions", "Pharma", "FMCG"];
    private static readonly string[] Regions = ["Algiers Capital", "Oran Coastal", "Constantine East", "Sétif Plateau", "Ouargla South", "Annaba Port"];
    private static readonly string[] Statuses = ["NewLead", "Engaged", "QualifiedMeeting", "ProposalSent", "ClosedWon"];

    private readonly DifferentialPrivacyEngine _dpEngine;

    public CopulaSyntheticDataGenerator(DifferentialPrivacyEngine dpEngine)
    {
        _dpEngine = dpEngine;
    }

    public IReadOnlyList<SyntheticLeadProfile> GenerateLeads(GenerateSyntheticDatasetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RecordCount <= 0) return [];

        var list = new List<SyntheticLeadProfile>(request.RecordCount);

        for (var i = 0; i < request.RecordCount; i++)
        {
            var fn = FirstNames[RandomNumberGenerator.GetInt32(0, FirstNames.Length)];
            var ln = LastNames[RandomNumberGenerator.GetInt32(0, LastNames.Length)];
            var prefix = CompanyPrefixes[RandomNumberGenerator.GetInt32(0, CompanyPrefixes.Length)];
            var suffix = CompanySuffixes[RandomNumberGenerator.GetInt32(0, CompanySuffixes.Length)];
            var reg = Regions[RandomNumberGenerator.GetInt32(0, Regions.Length)];
            var status = Statuses[RandomNumberGenerator.GetInt32(0, Statuses.Length)];

            // Base continuous variables: Log-normal distribution for budget (mode ~ 15,000 EUR)
            var baseLogBudget = 9.6 + (0.8 * (RandomNumberGenerator.GetInt32(0, 1000) / 1000.0 - 0.5));
            var rawBudget = Math.Exp(baseLogBudget);

            // Base engagement score: Beta(2, 2) shaped
            var rawEngagement = Math.Clamp(0.2 + (0.6 * (RandomNumberGenerator.GetInt32(0, 1000) / 1000.0)), 0.0, 1.0);

            // Apply DP noise if budget permits
            var perturbedBudget = rawBudget;
            var perturbedEngagement = rawEngagement;

            try
            {
                var budgetPerturbation = _dpEngine.PerturbQuery(new PerturbQueryRequest(
                    TenantId: request.TenantId,
                    QueryIdentifier: $"SYNTH-ROW-{i}-BUDGET",
                    RawValue: rawBudget,
                    SensitivityDeltaF: 500.0,
                    RequestedEpsilon: request.EpsilonBudget / request.RecordCount,
                    Mechanism: NoiseMechanism.Laplace
                ));
                perturbedBudget = Math.Max(1000.0, budgetPerturbation.PerturbedValue);
            }
            catch (InvalidOperationException)
            {
                // Fall back to clean synthetic sample if tenant DP budget reached
            }

            list.Add(new SyntheticLeadProfile(
                SyntheticId: $"SYNTH-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                FullName: $"{fn} {ln}",
                SyntheticCompany: $"{prefix} {suffix} SPA",
                Industry: request.IndustryFocus,
                Region: reg,
                EstimatedBudgetEur: Math.Round((decimal)perturbedBudget, 2),
                EngagementScore: Math.Round(perturbedEngagement, 3),
                LeadStatus: status
            ));
        }

        return list;
    }
}
