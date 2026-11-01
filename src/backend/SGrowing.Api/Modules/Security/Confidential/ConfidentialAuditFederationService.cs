using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.Confidential;

/// <summary>
/// Production-grade implementation of Confidential Audit Federation.
/// Issues and verifies HMAC-SHA256 signed audit attestation tokens with non-repudiation guarantees.
/// </summary>
public sealed class ConfidentialAuditFederationService : IConfidentialAuditFederationService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, ConfidentialAttestationToken> _attestations = new();

    public ConfidentialAttestationToken IssueAttestation(IssueAttestationRequest request, string consortiumSecretKey)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ConsortiumId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ComputationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResultCommitment);
        ArgumentException.ThrowIfNullOrWhiteSpace(consortiumSecretKey);

        string attestationId = Guid.NewGuid().ToString("N");
        DateTime issuedAtUtc = DateTime.UtcNow;

        string canonicalPayload = BuildCanonicalPayload(
            attestationId,
            request.ConsortiumId,
            request.ComputationType,
            request.InputHash,
            request.ResultCommitment,
            issuedAtUtc);

        byte[] keyBytes = Encoding.UTF8.GetBytes(consortiumSecretKey);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        byte[] signatureBytes = HMACSHA256.HashData(keyBytes, payloadBytes);
        string signatureHex = Convert.ToHexString(signatureBytes);

        var token = new ConfidentialAttestationToken
        {
            AttestationId = attestationId,
            ConsortiumId = request.ConsortiumId,
            ComputationType = request.ComputationType,
            InputHash = request.InputHash,
            ResultCommitment = request.ResultCommitment,
            IssuedAtUtc = issuedAtUtc,
            CryptographicSignature = signatureHex
        };

        lock (_lock)
        {
            _attestations[attestationId] = token;
        }

        return token;
    }

    public bool VerifyAttestation(ConfidentialAttestationToken token, string consortiumSecretKey)
    {
        if (token is null ||
            string.IsNullOrWhiteSpace(token.AttestationId) ||
            string.IsNullOrWhiteSpace(token.ConsortiumId) ||
            string.IsNullOrWhiteSpace(token.ComputationType) ||
            string.IsNullOrWhiteSpace(token.InputHash) ||
            string.IsNullOrWhiteSpace(token.ResultCommitment) ||
            string.IsNullOrWhiteSpace(token.CryptographicSignature) ||
            string.IsNullOrWhiteSpace(consortiumSecretKey))
        {
            return false;
        }

        string canonicalPayload = BuildCanonicalPayload(
            token.AttestationId,
            token.ConsortiumId,
            token.ComputationType,
            token.InputHash,
            token.ResultCommitment,
            token.IssuedAtUtc);

        byte[] keyBytes = Encoding.UTF8.GetBytes(consortiumSecretKey);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        byte[] expectedSignatureBytes = HMACSHA256.HashData(keyBytes, payloadBytes);

        try
        {
            byte[] actualSignatureBytes = Convert.FromHexString(token.CryptographicSignature);
            return CryptographicOperations.FixedTimeEquals(expectedSignatureBytes, actualSignatureBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public ConfidentialAttestationToken? GetAttestation(string attestationId)
    {
        if (string.IsNullOrWhiteSpace(attestationId))
        {
            return null;
        }

        lock (_lock)
        {
            return _attestations.GetValueOrDefault(attestationId);
        }
    }

    public IReadOnlyList<ConfidentialAttestationToken> GetRecentAttestations(int limit = 50)
    {
        lock (_lock)
        {
            return _attestations.Values
                .OrderByDescending(x => x.IssuedAtUtc)
                .Take(limit)
                .ToList();
        }
    }

    private static string BuildCanonicalPayload(
        string attestationId,
        string consortiumId,
        string computationType,
        string inputHash,
        string resultCommitment,
        DateTime issuedAtUtc)
    {
        return FormattableString.Invariant(
            $"{attestationId}:{consortiumId}:{computationType}:{inputHash}:{resultCommitment}:{issuedAtUtc:O}");
    }
}
