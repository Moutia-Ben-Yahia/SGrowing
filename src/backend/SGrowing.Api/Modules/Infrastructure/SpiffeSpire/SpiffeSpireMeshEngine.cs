using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SGrowing.Api.Modules.Infrastructure.SpiffeSpire;

public class SpiffeSpireMeshEngine
{
    private readonly ConcurrentDictionary<string, WorkloadSvidRecord> _svids = new();
    private readonly ConcurrentDictionary<string, TrustDomainFederationRecord> _federations = new();

    public SpiffeSpireMeshEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var s1 = new WorkloadSvidRecord(
            "SVID-001",
            "spiffe://sgrowing.enterprise/ns/growth-mesh/sa/telephony-whisper-engine",
            "growth-mesh",
            "telephony-whisper-engine",
            "K8S_PSAT",
            "SN-77A91F2B04E",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            "ACTIVE_VALID"
        );

        var s2 = new WorkloadSvidRecord(
            "SVID-002",
            "spiffe://sgrowing.enterprise/ns/finance-core/sa/sovereign-tax-ledger",
            "finance-core",
            "sovereign-tax-ledger",
            "AWS_IID",
            "SN-12C94F88A10",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            "ACTIVE_VALID"
        );

        _svids[s1.SvidId] = s1;
        _svids[s2.SvidId] = s2;

        var f1 = new TrustDomainFederationRecord(
            "FED-001",
            "sgrowing.enterprise",
            "aws.eu-central-1.corp",
            "https://spire.aws.eu-central-1.corp:8443/bundle",
            DateTime.UtcNow.AddMinutes(-5),
            "FEDERATED_HEALTHY"
        );

        var f2 = new TrustDomainFederationRecord(
            "FED-002",
            "sgrowing.enterprise",
            "gcp.europe-west1.corp",
            "https://spire.gcp.europe-west1.corp:8443/bundle",
            DateTime.UtcNow.AddMinutes(-3),
            "FEDERATED_HEALTHY"
        );

        _federations[f1.FederationId] = f1;
        _federations[f2.FederationId] = f2;
    }

    public SpiffeSpireOverview GetOverview()
    {
        var svidsList = _svids.Values.ToList();
        var fedList = _federations.Values.ToList();

        return new SpiffeSpireOverview(
            svidsList.Count,
            fedList.Count(f => f.Status == "FEDERATED_HEALTHY"),
            8.4,
            142,
            svidsList,
            fedList
        );
    }

    public WorkloadSvidRecord AttestWorkload(AttestWorkloadRequest request)
    {
        var svidId = $"SVID-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var spiffeId = $"spiffe://sgrowing.enterprise/ns/{request.AttestedNamespace}/sa/{request.ServiceAccount}";
        var serial = $"SN-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";

        var svid = new WorkloadSvidRecord(
            svidId,
            spiffeId,
            request.AttestedNamespace,
            request.ServiceAccount,
            request.NodeAttestationType,
            serial,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            "ACTIVE_VALID"
        );

        _svids[svidId] = svid;
        return svid;
    }

    public TrustDomainFederationRecord FederateDomain(FederateDomainRequest request)
    {
        var fedId = $"FED-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var record = new TrustDomainFederationRecord(
            fedId,
            "sgrowing.enterprise",
            request.RemoteTrustDomain,
            request.BundleEndpointUrl,
            DateTime.UtcNow,
            "FEDERATED_HEALTHY"
        );

        _federations[fedId] = record;
        return record;
    }
}
