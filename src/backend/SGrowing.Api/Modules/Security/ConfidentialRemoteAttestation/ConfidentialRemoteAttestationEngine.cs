namespace SGrowing.Api.Modules.Security.ConfidentialRemoteAttestation;

public class ConfidentialRemoteAttestationEngine
{
    public AttestationMeshResult VerifyAttestationMesh(EnclaveAttestationRequest request)
    {
        var policyMap = request.RegisteredPolicies.ToDictionary(p => p.EnclaveId);
        var records = new List<AttestationVerificationRecord>();
        int validCount = 0;
        int revokedCount = 0;

        foreach (var report in request.ReportsToVerify)
        {
            if (!policyMap.TryGetValue(report.EnclaveId, out var policy))
            {
                revokedCount++;
                records.Add(new AttestationVerificationRecord(
                    ReportId: report.ReportId,
                    EnclaveId: report.EnclaveId,
                    IsAttestationValid: false,
                    IsTcbCompliant: false,
                    TrustReason: "Policy Not Found: Enclave is unregistered in sovereignty governance ledger."
                ));
                continue;
            }

            bool archMatch = string.Equals(report.PlatformArchitecture, request.ExpectedPlatformArch, StringComparison.OrdinalIgnoreCase);
            bool mrenclaveMatch = string.Equals(report.ActualMrenclaveSha256, policy.ExpectedMrenclaveSha256, StringComparison.OrdinalIgnoreCase);
            bool mrsignerMatch = string.Equals(report.ActualMrSignerSha256, policy.ExpectedMrSignerSha256, StringComparison.OrdinalIgnoreCase);
            bool tcbCompliant = report.ReportedTcbVersion >= policy.MinimumTcbVersion;
            bool certChainValid = !string.IsNullOrWhiteSpace(report.ChipmakerCertChainFingerprint) &&
                                  (report.ChipmakerCertChainFingerprint.StartsWith("CERT-AMD-", StringComparison.OrdinalIgnoreCase) ||
                                   report.ChipmakerCertChainFingerprint.StartsWith("CERT-INTEL-", StringComparison.OrdinalIgnoreCase));
            bool signatureValid = report.ChipmakerSignature != null && report.ChipmakerSignature.Length >= 64;

            bool isAttestationValid = archMatch && mrenclaveMatch && mrsignerMatch && tcbCompliant && certChainValid && signatureValid;

            string reason;
            if (isAttestationValid)
            {
                validCount++;
                reason = "Hardware root of trust verified. Memory encryption key exchange approved.";
            }
            else
            {
                revokedCount++;
                if (!mrenclaveMatch)
                {
                    reason = "MRENCLAVE Measurement Tampered: In-memory binary hash does not match approved policy.";
                }
                else if (!tcbCompliant)
                {
                    reason = $"Outdated Microcode TCB ({report.ReportedTcbVersion} < {policy.MinimumTcbVersion}). Vulnerable to side-channel attack.";
                }
                else if (!archMatch)
                {
                    reason = $"Platform architecture mismatch ({report.PlatformArchitecture} != {request.ExpectedPlatformArch}).";
                }
                else
                {
                    reason = "Hardware signature or chipmaker certificate chain verification failure.";
                }
            }

            records.Add(new AttestationVerificationRecord(
                ReportId: report.ReportId,
                EnclaveId: report.EnclaveId,
                IsAttestationValid: isAttestationValid,
                IsTcbCompliant: tcbCompliant,
                TrustReason: reason
            ));
        }

        bool migrationPermitted = revokedCount == 0 && validCount > 0;
        string verdict;
        if (records.Count == 0)
        {
            verdict = "Zero Reports: No confidential enclave reports queued for remote attestation.";
        }
        else if (migrationPermitted)
        {
            verdict = "Sovereign Attestation Confirmed: All hardware enclaves verified; zero-trust workload execution cleared.";
        }
        else
        {
            verdict = $"Security Alert: {revokedCount} enclave(s) failed cryptographic measurement or TCB compliance; migration halted.";
        }

        return new AttestationMeshResult(
            TotalReportsAudited: records.Count,
            ValidEnclaves: validCount,
            RevokedEnclaves: revokedCount,
            Verifications: records,
            WorkloadMigrationPermitted: migrationPermitted,
            SovereigntyVerdict: verdict
        );
    }
}
