using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.Confidential;

/// <summary>
/// Production-grade implementation of Blind Private Set Intersection (PSI)
/// enabling privacy-preserving collaboration between consortium members.
/// </summary>
public sealed class PrivateSetIntersectionService : IPrivateSetIntersectionService
{
    public PsiIntersectionResult ComputeIntersection(
        IEnumerable<PsiCandidateItem> setA,
        IEnumerable<PsiCandidateItem> setB,
        string consortiumSessionId,
        IReadOnlyList<string>? participantIds = null)
    {
        ArgumentNullException.ThrowIfNull(setA);
        ArgumentNullException.ThrowIfNull(setB);
        ArgumentException.ThrowIfNullOrWhiteSpace(consortiumSessionId);

        long startTimestamp = Stopwatch.GetTimestamp();

        var listA = setA.ToList();
        var listB = setB.ToList();

        var hashesA = listA
            .Where(x => !string.IsNullOrWhiteSpace(x.BlindedHashHex))
            .Select(x => x.BlindedHashHex.Trim().ToUpperInvariant())
            .ToHashSet();

        var matched = listB
            .Where(x => !string.IsNullOrWhiteSpace(x.BlindedHashHex))
            .Select(x => x.BlindedHashHex.Trim().ToUpperInvariant())
            .Where(hashesA.Contains)
            .Distinct()
            .Order()
            .ToList();

        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        IReadOnlyList<string> resolvedParticipants = participantIds is { Count: > 0 }
            ? participantIds
            : listA.Select(x => x.PartitionKey)
                .Concat(listB.Select(x => x.PartitionKey))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .Order()
                .ToList();

        return new PsiIntersectionResult
        {
            MatchedCount = matched.Count,
            MatchedHashes = matched,
            ExecutionTimeMs = Math.Round(elapsedMs, 4),
            ConsortiumParticipantIds = resolvedParticipants
        };
    }

    public PsiIntersectionResult ComputeMultiPartyIntersection(
        IReadOnlyDictionary<string, IEnumerable<PsiCandidateItem>> participantSets,
        string consortiumSessionId)
    {
        ArgumentNullException.ThrowIfNull(participantSets);
        ArgumentException.ThrowIfNullOrWhiteSpace(consortiumSessionId);

        long startTimestamp = Stopwatch.GetTimestamp();

        if (participantSets.Count == 0)
        {
            return new PsiIntersectionResult
            {
                MatchedCount = 0,
                MatchedHashes = [],
                ExecutionTimeMs = 0,
                ConsortiumParticipantIds = []
            };
        }

        HashSet<string>? commonHashes = null;

        foreach (var (_, items) in participantSets)
        {
            var currentSet = items
                .Where(x => !string.IsNullOrWhiteSpace(x.BlindedHashHex))
                .Select(x => x.BlindedHashHex.Trim().ToUpperInvariant())
                .ToHashSet();

            if (commonHashes is null)
            {
                commonHashes = currentSet;
            }
            else
            {
                commonHashes.IntersectWith(currentSet);
            }
        }

        var matchedList = (commonHashes ?? []).Order().ToList();
        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        return new PsiIntersectionResult
        {
            MatchedCount = matchedList.Count,
            MatchedHashes = matchedList,
            ExecutionTimeMs = Math.Round(elapsedMs, 4),
            ConsortiumParticipantIds = participantSets.Keys.Order().ToList()
        };
    }

    public PsiCandidateItem BlindItem(string rawIdentifier, string partitionKey, string consortiumSessionSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(consortiumSessionSecret);

        byte[] keyBytes = Encoding.UTF8.GetBytes(consortiumSessionSecret);
        byte[] dataBytes = Encoding.UTF8.GetBytes(rawIdentifier.Trim().ToLowerInvariant());

        byte[] hash = HMACSHA256.HashData(keyBytes, dataBytes);

        return new PsiCandidateItem
        {
            BlindedHashHex = Convert.ToHexString(hash),
            PartitionKey = partitionKey
        };
    }

    public IReadOnlyList<PsiCandidateItem> BlindItems(
        IEnumerable<(string Identifier, string PartitionKey)> items,
        string consortiumSessionSecret)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(consortiumSessionSecret);

        byte[] keyBytes = Encoding.UTF8.GetBytes(consortiumSessionSecret);
        using var hmac = new HMACSHA256(keyBytes);

        var result = new List<PsiCandidateItem>();

        foreach (var (identifier, partitionKey) in items)
        {
            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(partitionKey))
            {
                continue;
            }

            byte[] dataBytes = Encoding.UTF8.GetBytes(identifier.Trim().ToLowerInvariant());
            byte[] hash = hmac.ComputeHash(dataBytes);

            result.Add(new PsiCandidateItem
            {
                BlindedHashHex = Convert.ToHexString(hash),
                PartitionKey = partitionKey
            });
        }

        return result;
    }
}
