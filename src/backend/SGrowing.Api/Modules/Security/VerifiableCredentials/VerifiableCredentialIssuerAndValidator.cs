using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.Security.VerifiableCredentials;

/// <summary>
/// W3C Verifiable Credential issuer and cryptographic verifier with RevocationList2020 bitstring status tracking.
/// </summary>
public sealed class VerifiableCredentialIssuerAndValidator
{
    private static readonly string[] DefaultContext =
    [
        "https://www.w3.org/2018/credentials/v1",
        "https://w3id.org/security/suites/jws-2020/v1",
        "https://schema.sgrowing.io/compliance/v1"
    ];

    private readonly ConcurrentDictionary<string, byte[]> _revocationBitstrings = new(StringComparer.OrdinalIgnoreCase);

    public VerifiableCredential IssueCredential(IssueCredentialRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var credentialId = $"urn:uuid:{Guid.NewGuid():D}";
        var now = DateTime.UtcNow;
        var expiration = request.ValidityDuration.HasValue ? now.Add(request.ValidityDuration.Value) : (DateTime?)null;

        CredentialStatus? status = null;
        if (request.RevocationListIndex.HasValue)
        {
            var listId = $"urn:sgrowing:revocation-list:{request.IssuerDid}";
            // Ensure bitstring exists (default 1024 bits = 128 bytes of zeros)
            _revocationBitstrings.GetOrAdd(listId, _ => new byte[128]);

            status = new CredentialStatus(
                Id: $"{listId}#{request.RevocationListIndex.Value}",
                Type: "RevocationList2020Status",
                RevocationListIndex: request.RevocationListIndex.Value,
                RevocationListCredentialUri: listId
            );
        }

        var subject = new CredentialSubject(
            Id: request.SubjectDid,
            VendorLegalName: request.VendorLegalName,
            TaxRegistrationNumber: request.TaxRegistrationNumber,
            CountryOfIncorporation: request.CountryOfIncorporation,
            CertificationType: request.CertificationType,
            CertificationAuthority: request.CertificationAuthority,
            Attributes: request.Attributes ?? new Dictionary<string, string>()
        );

        // Canonical digest to sign
        var canonicalData = CanonicalizeCredentialForSigning(
            credentialId,
            request.IssuerDid,
            now,
            expiration,
            subject,
            status
        );

        var signature = DidDocumentResolverEngine.SignData(request.IssuerPrivateKeySeed, canonicalData);
        var signatureHex = Convert.ToHexString(signature).ToLowerInvariant();

        var proof = new ProofObject(
            Type: "JsonWebKey2020",
            CreatedUtc: now,
            VerificationMethod: $"{request.IssuerDid}#key-1",
            ProofPurpose: "assertionMethod",
            ProofValueHex: signatureHex
        );

        return new VerifiableCredential(
            Id: credentialId,
            Context: DefaultContext,
            Type: ["VerifiableCredential", "VendorComplianceCredential", request.CertificationType.ToString()],
            IssuerDid: request.IssuerDid,
            IssuanceDateUtc: now,
            ExpirationDateUtc: expiration,
            CredentialSubject: subject,
            Status: status,
            Proof: proof
        );
    }

    public CredentialVerificationResult VerifyCredential(VerifiableCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        // 1. Expiration check
        if (credential.ExpirationDateUtc.HasValue && credential.ExpirationDateUtc.Value < DateTime.UtcNow)
        {
            return new CredentialVerificationResult(
                CredentialId: credential.Id,
                IsValid: false,
                Outcome: VerificationOutcome.Expired,
                Message: $"Credential expired on {credential.ExpirationDateUtc.Value:O}.",
                VerifiedAtUtc: DateTime.UtcNow
            );
        }

        // 2. Revocation status check
        if (credential.Status != null)
        {
            var isRevoked = CheckRevocationStatus(credential.Status);
            if (isRevoked)
            {
                return new CredentialVerificationResult(
                    CredentialId: credential.Id,
                    IsValid: false,
                    Outcome: VerificationOutcome.Revoked,
                    Message: $"Credential index {credential.Status.RevocationListIndex} is marked as revoked in status list.",
                    VerifiedAtUtc: DateTime.UtcNow
                );
            }
        }

        // 3. Cryptographic Signature Verification
        byte[] issuerPubKey;
        try
        {
            issuerPubKey = DidDocumentResolverEngine.ExtractPublicKeyFromDid(credential.IssuerDid);
        }
        catch (Exception ex)
        {
            return new CredentialVerificationResult(
                CredentialId: credential.Id,
                IsValid: false,
                Outcome: VerificationOutcome.IssuerUntrusted,
                Message: $"Unable to extract public key from issuer DID '{credential.IssuerDid}': {ex.Message}",
                VerifiedAtUtc: DateTime.UtcNow
            );
        }

        var canonicalData = CanonicalizeCredentialForSigning(
            credential.Id,
            credential.IssuerDid,
            credential.IssuanceDateUtc,
            credential.ExpirationDateUtc,
            credential.CredentialSubject,
            credential.Status
        );

        byte[] sigBytes;
        try
        {
            sigBytes = Convert.FromHexString(credential.Proof.ProofValueHex);
        }
        catch
        {
            return new CredentialVerificationResult(
                CredentialId: credential.Id,
                IsValid: false,
                Outcome: VerificationOutcome.Malformed,
                Message: "Proof signature is not a valid hexadecimal string.",
                VerifiedAtUtc: DateTime.UtcNow
            );
        }

        var validSig = DidDocumentResolverEngine.VerifySignature(issuerPubKey, canonicalData, sigBytes);
        if (!validSig)
        {
            return new CredentialVerificationResult(
                CredentialId: credential.Id,
                IsValid: false,
                Outcome: VerificationOutcome.InvalidSignature,
                Message: "Cryptographic signature validation failed. Payload has been tampered with or signed by unknown key.",
                VerifiedAtUtc: DateTime.UtcNow
            );
        }

        return new CredentialVerificationResult(
            CredentialId: credential.Id,
            IsValid: true,
            Outcome: VerificationOutcome.Valid,
            Message: "Credential signature, schema, and revocation status successfully verified.",
            VerifiedAtUtc: DateTime.UtcNow
        );
    }

    public void RevokeCredential(string revocationListUri, int index)
    {
        var bitstring = _revocationBitstrings.GetOrAdd(revocationListUri, _ => new byte[128]);
        var byteIdx = index / 8;
        var bitIdx = index % 8;

        if (byteIdx >= bitstring.Length)
        {
            var newBytes = new byte[byteIdx + 32];
            Buffer.BlockCopy(bitstring, 0, newBytes, 0, bitstring.Length);
            bitstring = newBytes;
            _revocationBitstrings[revocationListUri] = bitstring;
        }

        bitstring[byteIdx] |= (byte)(1 << bitIdx);
    }

    private bool CheckRevocationStatus(CredentialStatus status)
    {
        if (!_revocationBitstrings.TryGetValue(status.RevocationListCredentialUri, out var bitstring))
        {
            return false; // Not registered or empty list
        }

        var byteIdx = status.RevocationListIndex / 8;
        var bitIdx = status.RevocationListIndex % 8;

        if (byteIdx >= bitstring.Length) return false;

        return (bitstring[byteIdx] & (1 << bitIdx)) != 0;
    }

    private static byte[] CanonicalizeCredentialForSigning(
        string id,
        string issuer,
        DateTime issuance,
        DateTime? expiration,
        CredentialSubject subject,
        CredentialStatus? status)
    {
        var payload = new
        {
            id,
            issuer,
            issuance = issuance.ToString("O"),
            expiration = expiration?.ToString("O"),
            subject = new
            {
                subject.Id,
                subject.VendorLegalName,
                subject.TaxRegistrationNumber,
                subject.CountryOfIncorporation,
                CertificationType = subject.CertificationType.ToString(),
                subject.CertificationAuthority,
                subject.Attributes
            },
            status = status == null ? null : new
            {
                status.Id,
                status.Type,
                status.RevocationListIndex,
                status.RevocationListCredentialUri
            }
        };

        var json = JsonSerializer.Serialize(payload);
        return SHA256.HashData(Encoding.UTF8.GetBytes(json));
    }
}
