using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Marketing.Copywriting;

/// <summary>
/// Interface for recursive spintax matrix generation, combinatorial space computation,
/// Shannon entropy scoring, and thread-safe sliding window deduplication.
/// </summary>
public interface ISpintaxMatrixEngine
{
    /// <summary>
    /// Generates a single message variation from a spintax template, interpolating variables
    /// and enforcing thread-safe deduplication until the permutation space is saturated.
    /// </summary>
    string GenerateMessage(
        string template,
        int? seed = null,
        IDictionary<string, string>? variables = null,
        string? templateKey = null);

    /// <summary>
    /// Computes the exact combinatorial permutation space size for arbitrarily nested spintax expressions.
    /// </summary>
    long CalculatePermutationSpace(string template);

    /// <summary>
    /// Computes the Shannon entropy of generated outputs across sampled outputs to guarantee delivery diversity.
    /// </summary>
    double CalculateEntropy(string template, int sampleSize = 100);

    /// <summary>
    /// Resets the sliding window deduplication cache for a given template key or all keys if null.
    /// </summary>
    void ResetDeduplicationWindow(string? templateKey = null);
}

/// <summary>
/// Production engine for recursive spintax matrix evaluation, combinatorial permutation space analysis,
/// Shannon entropy verification, and thread-safe sliding window deduplication.
/// </summary>
public sealed class SpintaxMatrixEngine : ISpintaxMatrixEngine
{
    private readonly ConcurrentDictionary<string, DeduplicationWindow> _windows = new();
    private readonly Lock _globalLock = new();

    /// <inheritdoc />
    public string GenerateMessage(
        string template,
        int? seed = null,
        IDictionary<string, string>? variables = null,
        string? templateKey = null)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        // Apply pre-parsing variables for curly-braced variables like {name} or {{name}}
        string preparedTemplate = ApplyVariables(template, variables);

        var ast = SpintaxParser.Parse(preparedTemplate);
        long permutationSpace = ast.PermutationCount();

        // Deterministic seed mode: strictly reproduce based on seed
        if (seed.HasValue)
        {
            var deterministicRng = new Random(seed.Value);
            string resolved = ast.Resolve(deterministicRng);
            return ApplyVariables(resolved, variables);
        }

        // Non-seeded mode: use thread-safe sliding window deduplication
        string key = templateKey ?? ComputeTemplateKey(template);
        var window = _windows.GetOrAdd(key, _ => new DeduplicationWindow(capacity: 1000));

        if (permutationSpace <= 1)
        {
            string single = ast.Resolve(Random.Shared);
            string finalSingle = ApplyVariables(single, variables);
            window.TryRecordCandidate(finalSingle, permutationSpace, out _);
            return finalSingle;
        }

        // Attempt to find an unseen variation within retry limits
        int maxAttempts = Math.Min(100, Math.Max(20, (int)Math.Min(permutationSpace * 2, 100)));
        string candidate = string.Empty;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            candidate = ApplyVariables(ast.Resolve(Random.Shared), variables);
            if (window.TryRecordCandidate(candidate, permutationSpace, out bool isDuplicate) && !isDuplicate)
            {
                return candidate;
            }
        }

        // Permutation space saturated or attempts exhausted: emit candidate safely
        return candidate;
    }

    /// <inheritdoc />
    public long CalculatePermutationSpace(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return 1;
        }

        var ast = SpintaxParser.Parse(template);
        return Math.Max(1, ast.PermutationCount());
    }

    /// <inheritdoc />
    public double CalculateEntropy(string template, int sampleSize = 100)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return 0.0;
        }

        int actualSampleSize = Math.Max(10, sampleSize);
        var ast = SpintaxParser.Parse(template);
        long totalPermutations = ast.PermutationCount();

        if (totalPermutations <= 1)
        {
            return 0.0;
        }

        Dictionary<string, int> frequencies = [];
        var rng = new Random(42); // Fixed seed for reproducible entropy estimation

        for (int i = 0; i < actualSampleSize; i++)
        {
            string sample = ast.Resolve(rng);
            frequencies[sample] = frequencies.GetValueOrDefault(sample) + 1;
        }

        double entropy = 0.0;
        double n = actualSampleSize;

        foreach (int count in frequencies.Values)
        {
            double p = count / n;
            if (p > 0.0)
            {
                entropy -= p * Math.Log2(p);
            }
        }

        return Math.Round(Math.Max(0.0, entropy), 4);
    }

    /// <inheritdoc />
    public void ResetDeduplicationWindow(string? templateKey = null)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            lock (_globalLock)
            {
                foreach (var window in _windows.Values)
                {
                    window.Reset();
                }
                _windows.Clear();
            }
            return;
        }

        if (_windows.TryGetValue(templateKey, out var win))
        {
            win.Reset();
        }

        // Also check by hashed template key
        string hashedKey = ComputeTemplateKey(templateKey);
        if (_windows.TryGetValue(hashedKey, out var hashedWin))
        {
            hashedWin.Reset();
        }
    }

    private static string ComputeTemplateKey(string template)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(template));
        return Convert.ToHexString(bytes)[..16];
    }

    private static string ApplyVariables(string text, IDictionary<string, string>? variables)
    {
        if (variables is null || variables.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        string result = text;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal)
                           .Replace($"{{{key}}}", value, StringComparison.Ordinal)
                           .Replace($"${{{key}}}", value, StringComparison.Ordinal);
        }

        return result;
    }

    #region Internal AST and Recursive Parser

    internal interface ISpintaxNode
    {
        string Resolve(Random random);
        long PermutationCount();
    }

    internal sealed class TextNode(string text) : ISpintaxNode
    {
        public string Text { get; } = text;

        public string Resolve(Random random) => Text;

        public long PermutationCount() => 1;
    }

    internal sealed class SequenceNode(List<ISpintaxNode> children) : ISpintaxNode
    {
        public List<ISpintaxNode> Children { get; } = children;

        public string Resolve(Random random)
        {
            var sb = new StringBuilder();
            foreach (var child in Children)
            {
                sb.Append(child.Resolve(random));
            }
            return sb.ToString();
        }

        public long PermutationCount()
        {
            if (Children.Count == 0) return 1;

            long product = 1;
            foreach (var child in Children)
            {
                long count = Math.Max(1, child.PermutationCount());
                try
                {
                    checked
                    {
                        product *= count;
                    }
                }
                catch (OverflowException)
                {
                    return long.MaxValue;
                }
            }
            return product;
        }
    }

    internal sealed class ChoiceNode(List<ISpintaxNode> options) : ISpintaxNode
    {
        public List<ISpintaxNode> Options { get; } = options;

        public string Resolve(Random random)
        {
            if (Options.Count == 0) return string.Empty;
            int idx = random.Next(Options.Count);
            return Options[idx].Resolve(random);
        }

        public long PermutationCount()
        {
            if (Options.Count == 0) return 1;

            long sum = 0;
            foreach (var option in Options)
            {
                long count = Math.Max(1, option.PermutationCount());
                try
                {
                    checked
                    {
                        sum += count;
                    }
                }
                catch (OverflowException)
                {
                    return long.MaxValue;
                }
            }
            return Math.Max(1, sum);
        }
    }

    internal static class SpintaxParser
    {
        public static ISpintaxNode Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new TextNode(string.Empty);
            }

            int index = 0;
            return ParseSequence(input, ref index);
        }

        private static ISpintaxNode ParseSequence(string input, ref int index)
        {
            List<ISpintaxNode> sequence = [];
            var textBuffer = new StringBuilder();

            while (index < input.Length)
            {
                char c = input[index];

                if (c == '}' || c == '|')
                {
                    break;
                }

                if (c == '{')
                {
                    if (textBuffer.Length > 0)
                    {
                        sequence.Add(new TextNode(textBuffer.ToString()));
                        textBuffer.Clear();
                    }

                    index++; // Skip '{'
                    var choice = ParseChoice(input, ref index);
                    sequence.Add(choice);
                }
                else
                {
                    textBuffer.Append(c);
                    index++;
                }
            }

            if (textBuffer.Length > 0)
            {
                sequence.Add(new TextNode(textBuffer.ToString()));
            }

            if (sequence.Count == 0) return new TextNode(string.Empty);
            if (sequence.Count == 1) return sequence[0];
            return new SequenceNode(sequence);
        }

        private static ChoiceNode ParseChoice(string input, ref int index)
        {
            List<ISpintaxNode> options = [];

            while (index < input.Length)
            {
                var option = ParseSequence(input, ref index);
                options.Add(option);

                if (index < input.Length && input[index] == '|')
                {
                    index++; // Consume '|'
                    // If choice ends right after pipe like {A|}
                    if (index < input.Length && input[index] == '}')
                    {
                        options.Add(new TextNode(string.Empty));
                        index++; // Consume '}'
                        break;
                    }
                }
                else if (index < input.Length && input[index] == '}')
                {
                    index++; // Consume '}'
                    break;
                }
                else
                {
                    break;
                }
            }

            return new ChoiceNode(options);
        }
    }

    #endregion

    #region Deduplication Window

    private sealed class DeduplicationWindow(int capacity = 1000)
    {
        private readonly Lock _lock = new();
        private readonly int _capacity = capacity;
        private readonly Queue<string> _fifo = new();
        private readonly HashSet<string> _hashes = [];

        public bool TryRecordCandidate(string output, long totalPermutations, out bool isDuplicate)
        {
            lock (_lock)
            {
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(output)));

                if (_hashes.Contains(hash))
                {
                    // Check if space is saturated
                    if (totalPermutations > 0 && _hashes.Count >= totalPermutations)
                    {
                        isDuplicate = false; // Saturated: allow repetition
                        return true;
                    }

                    isDuplicate = true;
                    return false;
                }

                if (_fifo.Count >= _capacity)
                {
                    var oldest = _fifo.Dequeue();
                    _hashes.Remove(oldest);
                }

                _fifo.Enqueue(hash);
                _hashes.Add(hash);
                isDuplicate = false;
                return true;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _fifo.Clear();
                _hashes.Clear();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _hashes.Count;
                }
            }
        }
    }

    #endregion
}
