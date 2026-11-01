using System.Collections;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Field.Sync;

/// <summary>
/// A state-based Last-Write-Wins (LWW) Register CRDT.
/// Resolves concurrent writes deterministically by comparing timestamps; ties are broken
/// using ordinal string comparison of node IDs (higher node ID wins).
/// </summary>
/// <typeparam name="T">The contained payload value type.</typeparam>
public sealed class LwwRegister<T> : IEquatable<LwwRegister<T>>
{
    public T Value { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string NodeId { get; private set; }

    [JsonConstructor]
    public LwwRegister(T value, DateTimeOffset timestamp, string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        Value = value;
        Timestamp = timestamp;
        NodeId = nodeId;
    }

    public LwwRegister(T value) : this(value, DateTimeOffset.UtcNow, "default")
    {
    }

    /// <summary>
    /// Updates the register value if the incoming write has a newer timestamp (or wins tie-break).
    /// Mutates this register and returns it for fluent usage.
    /// </summary>
    public LwwRegister<T> Assign(T value, DateTimeOffset timestamp, string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (IsIncomingGreater(timestamp, nodeId))
        {
            Value = value;
            Timestamp = timestamp;
            NodeId = nodeId;
        }
        return this;
    }

    /// <summary>
    /// Merges this register with another register using LWW deterministic conflict resolution rules.
    /// Returns a new LwwRegister instance without mutating either input.
    /// </summary>
    public LwwRegister<T> Merge(LwwRegister<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsIncomingGreater(other.Timestamp, other.NodeId))
        {
            return new LwwRegister<T>(other.Value, other.Timestamp, other.NodeId);
        }
        return new LwwRegister<T>(this.Value, this.Timestamp, this.NodeId);
    }

    /// <summary>
    /// Compares incoming write coordinates against current coordinates.
    /// Returns true if incoming timestamp is greater, or if timestamps match and incoming nodeId is ordinally greater.
    /// </summary>
    private bool IsIncomingGreater(DateTimeOffset incomingTs, string incomingNodeId)
    {
        if (incomingTs > Timestamp) return true;
        if (incomingTs < Timestamp) return false;
        return string.Compare(incomingNodeId, NodeId, StringComparison.Ordinal) > 0;
    }

    public bool Equals(LwwRegister<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<T>.Default.Equals(Value, other.Value)
            && Timestamp == other.Timestamp
            && string.Equals(NodeId, other.NodeId, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is LwwRegister<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, Timestamp, NodeId);

    public override string ToString() => $"LwwRegister({Value}, {Timestamp:O}, {NodeId})";
}

/// <summary>
/// An element tagged with a unique operation UUID in an Add-Wins Observed-Remove Set (AW-ORSet).
/// </summary>
public sealed record ElementTag<T>(T Item, string Tag);

/// <summary>
/// A state-based Add-Wins Observed-Remove Set (AW-ORSet) CRDT.
/// Additions are tagged with unique operation UUIDs.
/// Removals observe and tombstone all current addition tags for the item.
/// A concurrent add with a new unobserved tag will win over a concurrent remove.
/// </summary>
/// <typeparam name="T">The set element type.</typeparam>
public sealed class AwOrSet<T> : IEnumerable<T>, IEquatable<AwOrSet<T>> where T : notnull
{
    private readonly HashSet<ElementTag<T>> _additions;
    private readonly HashSet<string> _removals;

    public AwOrSet()
    {
        _additions = [];
        _removals = new HashSet<string>(StringComparer.Ordinal);
    }

    [JsonConstructor]
    public AwOrSet(IEnumerable<ElementTag<T>>? additions, IEnumerable<string>? removals)
    {
        _additions = additions is not null ? [.. additions] : [];
        _removals = removals is not null ? new HashSet<string>(removals, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
    }

    public IReadOnlyCollection<ElementTag<T>> Additions => _additions;
    public IReadOnlySet<string> Removals => _removals;

    /// <summary>
    /// Gets the count of active elements in the set.
    /// </summary>
    public int Count => Elements.Count;

    /// <summary>
    /// Adds an element with an explicit unique operation tag.
    /// </summary>
    public AwOrSet<T> Add(T item, string tag)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        _additions.Add(new ElementTag<T>(item, tag));
        return this;
    }

    /// <summary>
    /// Adds an element generating an automatic UUID tag.
    /// Returns the generated operation tag.
    /// </summary>
    public string Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var tag = Guid.NewGuid().ToString("N");
        Add(item, tag);
        return tag;
    }

    /// <summary>
    /// Removes an element by observing and tombstoning all current addition tags associated with it.
    /// </summary>
    public AwOrSet<T> Remove(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        foreach (var entry in _additions)
        {
            if (EqualityComparer<T>.Default.Equals(entry.Item, item))
            {
                _removals.Add(entry.Tag);
            }
        }
        return this;
    }

    /// <summary>
    /// Returns true if the element has at least one active addition tag not present in removals.
    /// </summary>
    public bool Contains(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _additions.Any(e => EqualityComparer<T>.Default.Equals(e.Item, item) && !_removals.Contains(e.Tag));
    }

    /// <summary>
    /// Evaluates and returns the active elements of the set.
    /// </summary>
    [JsonIgnore]
    public IReadOnlySet<T> Elements => _additions
        .Where(e => !_removals.Contains(e.Tag))
        .Select(e => e.Item)
        .ToHashSet();

    /// <summary>
    /// Merges this AW-ORSet with another AW-ORSet.
    /// Returns a new instance where additions are unioned and removals are unioned (pure join-semilattice).
    /// </summary>
    public AwOrSet<T> Merge(AwOrSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var mergedAdds = new HashSet<ElementTag<T>>(_additions);
        mergedAdds.UnionWith(other._additions);

        var mergedRemovals = new HashSet<string>(_removals, StringComparer.Ordinal);
        mergedRemovals.UnionWith(other._removals);

        return new AwOrSet<T>(mergedAdds, mergedRemovals);
    }

    public IEnumerator<T> GetEnumerator() => Elements.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(AwOrSet<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _additions.SetEquals(other._additions) && _removals.SetEquals(other._removals);
    }

    public override bool Equals(object? obj) => obj is AwOrSet<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var add in _additions.OrderBy(a => a.Tag, StringComparer.Ordinal))
        {
            hash.Add(add);
        }
        foreach (var rem in _removals.OrderBy(r => r, StringComparer.Ordinal))
        {
            hash.Add(rem, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
}
