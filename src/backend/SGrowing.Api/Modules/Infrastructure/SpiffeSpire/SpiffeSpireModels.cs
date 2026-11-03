namespace SGrowing.Api.Modules.Infrastructure.SpiffeSpire;

public record WorkloadSvidRecord(
    string SvidId,
    string SpiffeId, // spiffe://sgrowing.enterprise/ns/{ns}/sa/{sa}
    string AttestedNamespace,
    string ServiceAccount,
    string NodeAttestationType, // K8S_PSAT, AWS_IID, GCP_IIT, AZURE_MSI
    string X509CertificateSerialNumber,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    string Status // ACTIVE_VALID, ROTATING, REVOKED
);

public record TrustDomainFederationRecord(
    string FederationId,
    string LocalTrustDomain, // sgrowing.enterprise
    string RemoteTrustDomain, // aws.eu-central-1.corp, gcp.europe-west1.corp
    string BundleEndpointUrl,
    DateTime LastBundleSync,
    string Status // FEDERATED_HEALTHY, DEGRADED, SYNCING
);

public record SpiffeSpireOverview(
    int ActiveSvidsCount,
    int FederatedTrustDomainsCount,
    double AverageAttestationLatencyMs, // e.g. 8.4ms
    int SvidRotationsLast24Hours,
    List<WorkloadSvidRecord> ActiveSvids,
    List<TrustDomainFederationRecord> FederatedDomains
);

public record AttestWorkloadRequest(
    string AttestedNamespace,
    string ServiceAccount,
    string NodeAttestationType
);

public record FederateDomainRequest(
    string RemoteTrustDomain,
    string BundleEndpointUrl
);
