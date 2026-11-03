using SGrowing.Api.Modules.Infrastructure.SpiffeSpire;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class SpiffeSpireTests
{
    [Fact]
    public void GetOverview_ReportsActiveSvidsAndFederatedTrustDomains()
    {
        var engine = new SpiffeSpireMeshEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveSvidsCount >= 2);
        Assert.True(overview.FederatedTrustDomainsCount >= 2);
        Assert.True(overview.AverageAttestationLatencyMs < 15.0);
        Assert.True(overview.SvidRotationsLast24Hours > 0);
        Assert.NotEmpty(overview.ActiveSvids);
        Assert.NotEmpty(overview.FederatedDomains);
    }

    [Fact]
    public void AttestWorkload_IssuesShortLivedX509SvidWithSpiffeId()
    {
        var engine = new SpiffeSpireMeshEngine();
        var request = new AttestWorkloadRequest(
            "production-gateway",
            "bgp-bfd-failover-daemon",
            "K8S_PSAT"
        );

        var svid = engine.AttestWorkload(request);

        Assert.NotNull(svid);
        Assert.StartsWith("SVID-", svid.SvidId);
        Assert.Equal("spiffe://sgrowing.enterprise/ns/production-gateway/sa/bgp-bfd-failover-daemon", svid.SpiffeId);
        Assert.StartsWith("SN-", svid.X509CertificateSerialNumber);
        Assert.Equal("ACTIVE_VALID", svid.Status);
        Assert.True(svid.ExpiresAt > DateTime.UtcNow);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveSvids, s => s.SvidId == svid.SvidId);
    }

    [Fact]
    public void FederateDomain_SyncsRemoteTrustDomainBundle()
    {
        var engine = new SpiffeSpireMeshEngine();
        var request = new FederateDomainRequest(
            "azure.westeurope.corp",
            "https://spire.azure.westeurope.corp:8443/bundle"
        );

        var fed = engine.FederateDomain(request);

        Assert.NotNull(fed);
        Assert.StartsWith("FED-", fed.FederationId);
        Assert.Equal("azure.westeurope.corp", fed.RemoteTrustDomain);
        Assert.Equal("FEDERATED_HEALTHY", fed.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.FederatedDomains, f => f.FederationId == fed.FederationId);
    }
}
