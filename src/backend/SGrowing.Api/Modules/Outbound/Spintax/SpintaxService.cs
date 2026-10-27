using System.Text;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Outbound.Spintax;

public class SpintaxService : ISpintaxService
{
    private static readonly Regex SpintaxRegex = new(@"\{([^{}]*\|[^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex VariableRegex = new(@"\{\{?([a-zA-Z0-9_]+)\}?\}", RegexOptions.Compiled);

    public string Parse(string template, int? seed = null, IDictionary<string, string>? variables = null)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var processed = template;

        // Iteratively resolve innermost spintax alternative groups ({A|B}) until none remain
        while (SpintaxRegex.IsMatch(processed))
        {
            var previous = processed;
            processed = SpintaxRegex.Replace(processed, match =>
            {
                var content = match.Groups[1].Value;
                var options = content.Split('|');
                return options[random.Next(options.Length)];
            });

            // Guard against infinite loop if no substitution changed the content
            if (string.Equals(previous, processed, StringComparison.Ordinal))
            {
                break;
            }
        }

        // Interpolate remaining variables if provided (e.g. {FirstName} or {{FirstName}})
        if (variables != null && variables.Count > 0)
        {
            processed = VariableRegex.Replace(processed, match =>
            {
                var varName = match.Groups[1].Value;
                return variables.TryGetValue(varName, out var val) ? val : match.Value;
            });
        }

        return processed;
    }

    public string ParseForProspect(string template, string prospectIdOrEmail, IDictionary<string, string>? variables = null)
    {
        var seed = GetDeterministicSeed(prospectIdOrEmail);
        return Parse(template, seed, variables);
    }

    public IReadOnlyList<string> GenerateVariants(string template, int count, IDictionary<string, string>? variables = null)
    {
        if (count <= 0) return [];
        var variants = new HashSet<string>(StringComparer.Ordinal);
        
        // Try up to count * 10 iterations to get unique variants
        for (int i = 0; i < count * 10 && variants.Count < count; i++)
        {
            var variant = Parse(template, seed: i * 37 + 101, variables: variables);
            variants.Add(variant);
        }

        return variants.ToList();
    }

    public long CalculatePermutationsCount(string template)
    {
        if (string.IsNullOrEmpty(template)) return 0;
        return CountPermutationsRecursive(template);
    }

    private static long CountPermutationsRecursive(string input)
    {
        long total = 1;
        int i = 0;
        while (i < input.Length)
        {
            if (input[i] == '{')
            {
                int depth = 1;
                int start = i + 1;
                int j = start;
                var parts = new List<string>();
                int partStart = start;

                while (j < input.Length && depth > 0)
                {
                    if (input[j] == '{') depth++;
                    else if (input[j] == '}') depth--;
                    
                    if ((input[j] == '|' && depth == 1) || (input[j] == '}' && depth == 0))
                    {
                        parts.Add(input.Substring(partStart, j - partStart));
                        partStart = j + 1;
                    }
                    j++;
                }

                if (parts.Count > 1)
                {
                    long subCount = 0;
                    foreach (var part in parts)
                    {
                        subCount += CountPermutationsRecursive(part);
                    }
                    total *= Math.Max(1, subCount);
                }

                i = j;
            }
            else
            {
                i++;
            }
        }

        return total;
    }

    private static int GetDeterministicSeed(string input)
    {
        if (string.IsNullOrEmpty(input)) return 42;
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in input.ToLowerInvariant())
            {
                hash = (hash ^ c) * 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
