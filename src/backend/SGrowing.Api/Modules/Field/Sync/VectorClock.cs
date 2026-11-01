using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Field.Sync;

/// <summary>
/// Represents the causal relationship between two vector clocks.
/// </summary>
public enum CausalityRelation
{
    Equal = 0,
    Dominates = 1,
    HappensAfter = 1,
    Dominated = 2,
    HappensBefore = 2,
    Concurrent = 3,
    Conflict = 3
}

/// <summary>
/// A logical Lamport vector clock for distributed causal ordering and conflict detection across edge nodes.
/// </summary>
[JsonConverter(typeof(VectorClockJsonConverter))]
public sealed class VectorClock : IEquatable<VectorClock>
{
    private readonly Dictionary<string, long> _clock;

    public VectorClock()
    {
        _clock = new Dictionary<string, long>(StringComparer.Ordinal);
    }

    public VectorClock(IDictionary<string, long> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _clock = new Dictionary<string, long>(entries, StringComparer.Ordinal);
    }

    public VectorClock(VectorClock other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _clock = new Dictionary<string, long>(other._clock, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the current dictionary of node IDs to Lamport logical counters.
    /// </summary>
    public IReadOnlyDictionary<string, long> Entries => _clock;

    /// <summary>
    /// Gets the logical counter for the specified node ID, returning 0 if not present.
    /// </summary>
    public long this[string nodeId] => _clock.GetValueOrDefault(nodeId, 0L);

    /// <summary>
    /// Gets the logical counter for the specified node ID.
    /// </summary>
    public long Get(string nodeId) => this[nodeId];

    /// <summary>
    /// Increments the logical counter for the given node ID by 1.
    /// Mutates this instance and returns it for fluent chaining.
    /// </summary>
    public VectorClock Increment(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        _clock[nodeId] = _clock.GetValueOrDefault(nodeId, 0L) + 1L;
        return this;
    }

    /// <summary>
    /// Returns a new VectorClock instance with the given node ID incremented.
    /// </summary>
    public VectorClock WithIncrement(string nodeId)
    {
        var copy = Clone();
        copy.Increment(nodeId);
        return copy;
    }

    /// <summary>
    /// Creates a deep copy of this vector clock.
    /// </summary>
    public VectorClock Clone() => new(_clock);

    /// <summary>
    /// Merges this clock with another vector clock by taking the maximum counter for each node.
    /// Returns a new VectorClock without mutating either input (pure join-semilattice operator).
    /// </summary>
    public VectorClock Merge(VectorClock other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var merged = new Dictionary<string, long>(_clock, StringComparer.Ordinal);
        foreach (var (k, v) in other._clock)
        {
            merged[k] = Math.Max(merged.GetValueOrDefault(k, 0L), v);
        }
        return new VectorClock(merged);
    }

    /// <summary>
    /// Merges another clock into this instance in-place and returns this clock.
    /// </summary>
    public VectorClock MergeInPlace(VectorClock other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var (k, v) in other._clock)
        {
            _clock[k] = Math.Max(_clock.GetValueOrDefault(k, 0L), v);
        }
        return this;
    }

    /// <summary>
    /// Determines the causal relationship between this clock and another clock.
    /// </summary>
    public CausalityRelation CompareTo(VectorClock other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var allKeys = new HashSet<string>(_clock.Keys, StringComparer.Ordinal);
        allKeys.UnionWith(other._clock.Keys);

        bool hasGreater = false;
        bool hasLesser = false;

        foreach (var key in allKeys)
        {
            var v1 = _clock.GetValueOrDefault(key, 0L);
            var v2 = other._clock.GetValueOrDefault(key, 0L);

            if (v1 > v2) hasGreater = true;
            if (v1 < v2) hasLesser = true;

            if (hasGreater && hasLesser)
            {
                return CausalityRelation.Concurrent;
            }
        }

        if (hasGreater && !hasLesser) return CausalityRelation.Dominates;
        if (hasLesser && !hasGreater) return CausalityRelation.Dominated;
        return CausalityRelation.Equal;
    }

    /// <summary>
    /// Returns true if this clock dominates (happens after) the other clock.
    /// </summary>
    public bool Dominates(VectorClock other) => CompareTo(other) == CausalityRelation.Dominates;

    /// <summary>
    /// Returns true if this clock is dominated by (happens before) the other clock.
    /// </summary>
    public bool IsDominatedBy(VectorClock other) => CompareTo(other) == CausalityRelation.Dominated;

    /// <summary>
    /// Returns true if the two clocks are concurrent (conflict).
    /// </summary>
    public bool IsConcurrentWith(VectorClock other) => CompareTo(other) == CausalityRelation.Concurrent;

    /// <summary>
    /// Exports the clock as a mutable dictionary copy.
    /// </summary>
    public Dictionary<string, long> ToDictionary() => new(_clock, StringComparer.Ordinal);

    /// <summary>
    /// Creates a VectorClock from a dictionary.
    /// </summary>
    public static VectorClock FromDictionary(IDictionary<string, long> dict) => new(dict);

    /// <summary>
    /// Serializes the vector clock to a JSON string.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(_clock);

    /// <summary>
    /// Deserializes a vector clock from a JSON string.
    /// </summary>
    public static VectorClock FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var dict = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
        return new VectorClock(dict ?? []);
    }

    /// <summary>
    /// Parses a vector clock from JSON format ("{\"node1\":1}") or key-value list ("node1:1,node2:2").
    /// </summary>
    public static VectorClock Parse(string str)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(str);
        var trimmed = str.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return FromJson(trimmed);
        }

        var clock = new VectorClock();
        var pairs = trimmed.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split([':', '='], 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && long.TryParse(parts[1], out var val))
            {
                clock._clock[parts[0]] = val;
            }
        }
        return clock;
    }

    /// <summary>
    /// Attempts to parse a vector clock string.
    /// </summary>
    public static bool TryParse(string? str, out VectorClock? clock)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            clock = null;
            return false;
        }
        try
        {
            clock = Parse(str);
            return true;
        }
        catch
        {
            clock = null;
            return false;
        }
    }

    public override string ToString() => ToJson();

    public bool Equals(VectorClock? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CompareTo(other) == CausalityRelation.Equal;
    }

    public override bool Equals(object? obj) => obj is VectorClock other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (k, v) in _clock.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (v != 0)
            {
                hash.Add(k, StringComparer.Ordinal);
                hash.Add(v);
            }
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(VectorClock? left, VectorClock? right) => Equals(left, right);
    public static bool operator !=(VectorClock? left, VectorClock? right) => !Equals(left, right);
}

/// <summary>
/// JSON converter for serializing VectorClock directly as a JSON dictionary of nodeId -> counter.
/// </summary>
public sealed class VectorClockJsonConverter : JsonConverter<VectorClock>
{
    public override VectorClock Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, long>>(ref reader, options);
        return new VectorClock(dict ?? []);
    }

    public override void Write(Utf8JsonWriter writer, VectorClock value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Entries, options);
    }
}
