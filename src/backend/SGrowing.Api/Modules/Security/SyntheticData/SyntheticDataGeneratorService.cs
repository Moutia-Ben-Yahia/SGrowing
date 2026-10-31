using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.SyntheticData;

#region Domain Enums & Models

public enum DataClassification
{
    DirectIdentifier,   // Names, SSN, Tax ID, Passport, Phone, Email
    QuasiIdentifier,    // Postal Code, Job Title, Age, IP Address
    SensitiveAttribute, // Salaries, Balances, Credit Ratings, Health Data
    NonSensitive        // Status, Dates, Category Codes, Booleans
}

public enum SynthesisStrategy
{
    RealisticFake,
    FormatPreservingToken,
    DifferentialPrivacyNoise,
    DeterministicHash,
    ConstantRedacted
}

public record ColumnSchemaRule(
    string ColumnName,
    DataClassification Classification,
    SynthesisStrategy Strategy,
    string DataType = "string",
    double? DifferentialPrivacyEpsilon = null,
    string? FormatPattern = null
);

public record GenerateSyntheticDatasetRequest(
    Guid TenantId,
    string TableName,
    int RowCount,
    List<ColumnSchemaRule> ColumnRules,
    int RandomSeed = 42
);

public record SyntheticGenerationSummary(
    Guid JobId,
    Guid TenantId,
    string TableName,
    int RowCount,
    int DirectIdentifiersMasked,
    int QuasiIdentifiersPerturbed,
    List<Dictionary<string, object?>> SampleRecords,
    string SqlInsertScript,
    DateTime GeneratedAtUtc
);

#endregion

public interface ISyntheticDataGeneratorService
{
    Task<SyntheticGenerationSummary> GenerateSyntheticDatasetAsync(
        GenerateSyntheticDatasetRequest request,
        CancellationToken ct = default);

    Task<List<ColumnSchemaRule>> InspectAndRecommendRulesAsync(
        List<string> columnNames,
        CancellationToken ct = default);
}

public class SyntheticDataGeneratorService : ISyntheticDataGeneratorService
{
    private static readonly string[] FirstNames = { "Alex", "Jordan", "Taylor", "Morgan", "Sam", "Chris", "Pat", "Robin", "Casey", "Avery" };
    private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };
    private static readonly string[] Domains = { "company.test", "sandbox.corp", "enterprise.dev", "mockmail.internal" };
    private static readonly string[] Cities = { "New York", "London", "Paris", "Berlin", "Tokyo", "Singapore", "Sydney", "Toronto" };
    private static readonly string[] JobTitles = { "Software Engineer", "Operations Lead", "Financial Analyst", "VP of Sales", "Product Director" };

    public Task<SyntheticGenerationSummary> GenerateSyntheticDatasetAsync(
        GenerateSyntheticDatasetRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            throw new ArgumentException("Table name cannot be empty.", nameof(request));
        }

        if (request.RowCount <= 0 || request.RowCount > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "RowCount must be between 1 and 5000.");
        }

        if (request.ColumnRules == null || request.ColumnRules.Count == 0)
        {
            throw new ArgumentException("At least one column rule must be specified.", nameof(request));
        }

        var rng = new Random(request.RandomSeed);
        var records = new List<Dictionary<string, object?>>();

        int directIdentCount = request.ColumnRules.Count(c => c.Classification == DataClassification.DirectIdentifier);
        int quasiIdentCount = request.ColumnRules.Count(c => c.Classification == DataClassification.QuasiIdentifier);

        for (int i = 0; i < request.RowCount; i++)
        {
            var row = new Dictionary<string, object?>();
            string first = FirstNames[rng.Next(FirstNames.Length)];
            string last = LastNames[rng.Next(LastNames.Length)];

            foreach (var rule in request.ColumnRules)
            {
                object? val = GenerateColumnValue(rule, first, last, i, rng);
                row[rule.ColumnName] = val;
            }
            records.Add(row);
        }

        // Generate SQL insert script
        var sb = new StringBuilder();
        sb.AppendLine($"-- Synthetic Seed Data for [{request.TableName}] (Generated: {DateTime.UtcNow:O})");
        sb.AppendLine($"-- Direct Identifiers Masked: {directIdentCount}, Differential Privacy Applied: {quasiIdentCount}");
        sb.AppendLine();

        var colNames = string.Join(", ", request.ColumnRules.Select(c => $"[{c.ColumnName}]"));
        foreach (var rec in records.Take(100)) // Script snippet up to 100 rows
        {
            var values = string.Join(", ", rec.Values.Select(FormatSqlLiteral));
            sb.AppendLine($"INSERT INTO [{request.TableName}] ({colNames}) VALUES ({values});");
        }

        var summary = new SyntheticGenerationSummary(
            Guid.NewGuid(),
            request.TenantId,
            request.TableName,
            request.RowCount,
            directIdentCount,
            quasiIdentCount,
            records.Take(15).ToList(),
            sb.ToString(),
            DateTime.UtcNow
        );

        return Task.FromResult(summary);
    }

    public Task<List<ColumnSchemaRule>> InspectAndRecommendRulesAsync(
        List<string> columnNames,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(columnNames);

        var list = new List<ColumnSchemaRule>();
        foreach (var rawCol in columnNames)
        {
            var col = rawCol.Trim();
            var colLower = col.ToLowerInvariant();

            if (colLower.Contains("name") || colLower.Contains("fullname") || colLower.Contains("first") || colLower.Contains("last"))
            {
                list.Add(new ColumnSchemaRule(col, DataClassification.DirectIdentifier, SynthesisStrategy.RealisticFake, "string"));
            }
            else if (colLower.Contains("email"))
            {
                list.Add(new ColumnSchemaRule(col, DataClassification.DirectIdentifier, SynthesisStrategy.RealisticFake, "string"));
            }
            else if (colLower.Contains("phone") || colLower.Contains("mobile") || colLower.Contains("fax"))
            {
                list.Add(new ColumnSchemaRule(col, DataClassification.DirectIdentifier, SynthesisStrategy.RealisticFake, "string"));
            }
            else if (colLower.Contains("ssn") || colLower.Contains("tax") || colLower.Contains("cin") || colLower.Contains("passport"))
            {
                list.Add(new ColumnSchemaRule(col, DataClassification.DirectIdentifier, SynthesisStrategy.FormatPreservingToken, "string"));
            }
            else if (colLower.Contains("salary") || colLower.Contains("balance") || colLower.Contains("amount") || colLower.Contains("revenue"))
            {
                list.Add(new ColumnSchemaRule(col, DataClassification.SensitiveAttribute, SynthesisStrategy.DifferentialPrivacyNoise, "decimal", 0.5));
            }
            else if (colLower.Contains("zip") || colLower.Contains("postal") || colLower.Contains("city") || colLower.Contains("job") || colLower.Contains("title"))
            {
                list.Add(new ColumnSchemaRule(col, DataClassification.QuasiIdentifier, SynthesisStrategy.RealisticFake, "string"));
            }
            else
            {
                list.Add(new ColumnSchemaRule(col, DataClassification.NonSensitive, SynthesisStrategy.RealisticFake, "string"));
            }
        }

        return Task.FromResult(list);
    }

    private object? GenerateColumnValue(ColumnSchemaRule rule, string first, string last, int rowIndex, Random rng)
    {
        var colLower = rule.ColumnName.ToLowerInvariant();

        switch (rule.Strategy)
        {
            case SynthesisStrategy.ConstantRedacted:
                return "[REDACTED]";

            case SynthesisStrategy.DeterministicHash:
                using (var sha = SHA256.Create())
                {
                    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{first}_{last}_{rowIndex}"));
                    return Convert.ToHexString(bytes)[..16];
                }

            case SynthesisStrategy.FormatPreservingToken:
                if (colLower.Contains("ssn") || colLower.Contains("tax"))
                {
                    return $"{rng.Next(100, 999)}-{rng.Next(10, 99)}-{rng.Next(1000, 9999)}";
                }
                return $"TOK-{rng.Next(100000, 999999)}";

            case SynthesisStrategy.DifferentialPrivacyNoise:
                // Inject Laplace perturbation noise
                double epsilon = rule.DifferentialPrivacyEpsilon ?? 0.5;
                double scale = 1.0 / Math.Max(0.01, epsilon);
                double u = rng.NextDouble() - 0.5;
                double laplaceNoise = -scale * Math.Sign(u) * Math.Log(1.0 - (2.0 * Math.Abs(u)));

                double baseVal = 5000.0 + (rng.Next(1, 100) * 150.0);
                double perturbed = Math.Max(0.0, baseVal + laplaceNoise * 100.0);
                return Math.Round(perturbed, 2);

            case SynthesisStrategy.RealisticFake:
            default:
                if (colLower.Contains("email"))
                {
                    return $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}{rng.Next(10, 99)}@{Domains[rng.Next(Domains.Length)]}";
                }
                if (colLower.Contains("phone") || colLower.Contains("mobile"))
                {
                    return $"+1 (555) {rng.Next(200, 999)}-{rng.Next(1000, 9999)}";
                }
                if (colLower.Contains("city"))
                {
                    return Cities[rng.Next(Cities.Length)];
                }
                if (colLower.Contains("job") || colLower.Contains("title"))
                {
                    return JobTitles[rng.Next(JobTitles.Length)];
                }
                if (colLower.Contains("first"))
                {
                    return first;
                }
                if (colLower.Contains("last"))
                {
                    return last;
                }
                if (colLower.Contains("name"))
                {
                    return $"{first} {last}";
                }
                if (rule.DataType == "decimal" || rule.DataType == "double")
                {
                    return Math.Round(rng.NextDouble() * 500.0 + 10.0, 2);
                }
                if (rule.DataType == "int")
                {
                    return rng.Next(1, 1000);
                }
                if (rule.DataType == "bool")
                {
                    return rng.Next(2) == 1;
                }
                return $"Val_{rowIndex + 1}";
        }
    }

    private static string FormatSqlLiteral(object? value)
    {
        if (value == null) return "NULL";
        if (value is bool b) return b ? "1" : "0";
        if (value is int or long or double or decimal or float) return value.ToString()!;
        return $"'{value.ToString()!.Replace("'", "''")}'";
    }
}
