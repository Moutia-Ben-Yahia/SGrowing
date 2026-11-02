namespace SGrowing.Api.Modules.Security.VerifiableCredentials;

/// <summary>
/// Autonomous gateway evaluating B2B vendor onboarding credentials against
/// required regulatory and domain compliance baselines.
/// </summary>
public sealed class VendorComplianceVerificationGateway
{
    private readonly VerifiableCredentialIssuerAndValidator _validator;

    public VendorComplianceVerificationGateway(VerifiableCredentialIssuerAndValidator validator)
    {
        _validator = validator;
    }

    public VendorComplianceReport AssessVendorCompliance(
        string vendorDid,
        string vendorLegalName,
        IReadOnlyList<CredentialComplianceType> requiredCertifications,
        IReadOnlyList<VerifiableCredential> presentedCredentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorDid);
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorLegalName);
        ArgumentNullException.ThrowIfNull(requiredCertifications);
        ArgumentNullException.ThrowIfNull(presentedCredentials);

        var auditResults = new List<CredentialVerificationResult>();
        var verifiedTypes = new HashSet<CredentialComplianceType>();

        foreach (var cred in presentedCredentials)
        {
            // Ensure credential belongs to this vendor
            if (!string.Equals(cred.CredentialSubject.Id, vendorDid, StringComparison.OrdinalIgnoreCase))
            {
                auditResults.Add(new CredentialVerificationResult(
                    CredentialId: cred.Id,
                    IsValid: false,
                    Outcome: VerificationOutcome.Malformed,
                    Message: $"Subject DID '{cred.CredentialSubject.Id}' does not match candidate vendor DID '{vendorDid}'.",
                    VerifiedAtUtc: DateTime.UtcNow
                ));
                continue;
            }

            var verification = _validator.VerifyCredential(cred);
            auditResults.Add(verification);

            if (verification.IsValid)
            {
                verifiedTypes.Add(cred.CredentialSubject.CertificationType);
            }
        }

        var missing = requiredCertifications
            .Where(req => !verifiedTypes.Contains(req))
            .ToList();

        VendorComplianceStatus status;
        if (missing.Count == 0)
        {
            status = VendorComplianceStatus.Compliant;
        }
        else if (verifiedTypes.Count > 0 && verifiedTypes.Contains(CredentialComplianceType.CommercialTaxRegistration))
        {
            // Has tax registration but missing some secondary certifications -> conditional
            status = VendorComplianceStatus.ConditionalApproval;
        }
        else
        {
            status = VendorComplianceStatus.NonCompliant;
        }

        return new VendorComplianceReport(
            VendorDid: vendorDid,
            VendorLegalName: vendorLegalName,
            Status: status,
            VerifiedCertifications: verifiedTypes.ToList(),
            MissingCertifications: missing,
            AuditResults: auditResults,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }
}
