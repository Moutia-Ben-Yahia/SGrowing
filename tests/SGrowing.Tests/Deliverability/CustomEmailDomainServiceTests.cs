using System.Security.Cryptography;
using System.Text.Json;
using SGrowing.Api.Modules.Deliverability;
using Xunit;

namespace SGrowing.Tests.Deliverability;

public class CustomEmailDomainServiceTests
{
    private readonly CustomEmailDomainService _service;

    public CustomEmailDomainServiceTests()
    {
        _service = new CustomEmailDomainService();
    }

    [Fact]
    public void GenerateDkimKeyPair_Creates2048BitRsaKey()
    {
        // Act
        var dkim = _service.GenerateDkimKeyPair("corp.customer.io", "sgrowing");

        // Assert
        Assert.NotNull(dkim);
        Assert.Equal("sgrowing", dkim.Selector);
        Assert.Equal(2048, dkim.KeyLengthBits);

        // Verify public key can be imported into RSA
        byte[] pubBytes = Convert.FromBase64String(dkim.PublicKeyBase64);
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(pubBytes, out _);
        Assert.Equal(2048, rsa.KeySize);
    }

    [Fact]
    public void GenerateDkimKeyPair_ProducesValidBase64PublicKeyAndPrivateKey()
    {
        // Act
        var dkim = _service.GenerateDkimKeyPair("client.io");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(dkim.PublicKeyBase64));
        Assert.False(string.IsNullOrWhiteSpace(dkim.PrivateKeyPemOrBase64));

        byte[] privBytes = Convert.FromBase64String(dkim.PrivateKeyPemOrBase64);
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privBytes, out _);
        Assert.Equal(2048, rsa.KeySize);
    }

    [Fact]
    public void GenerateDkimKeyPair_CalculatesSha256Fingerprint()
    {
        // Act
        var dkim = _service.GenerateDkimKeyPair("enterprise.com");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(dkim.KeyFingerprint));
        // SHA-256 formatted as 16 colon-separated hex bytes
        var parts = dkim.KeyFingerprint.Split(':');
        Assert.Equal(16, parts.Length);
        Assert.All(parts, p => Assert.True(p.Length == 2 && int.TryParse(p, System.Globalization.NumberStyles.HexNumber, null, out _)));
    }

    [Fact]
    public void GenerateDkimKeyPair_FormatsTxtRecordCorrectly()
    {
        // Act
        var dkim = _service.GenerateDkimKeyPair("mail.domain.org");

        // Assert
        Assert.StartsWith("v=DKIM1; k=rsa; p=", dkim.TxtRecordValue);
        Assert.EndsWith(dkim.PublicKeyBase64, dkim.TxtRecordValue);
    }

    [Fact]
    public async Task RegisterDomainAsync_ValidDomain_GeneratesCompleteDnsRecordSet()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var uniqueDomain = $"send-{Guid.NewGuid():N}.example.com";
        var dto = new RegisterCustomDomainDto(uniqueDomain);

        // Act
        var domain = await _service.RegisterDomainAsync(tenantId, dto);

        // Assert
        Assert.NotNull(domain);
        Assert.Equal(tenantId, domain.TenantId);
        Assert.Equal(uniqueDomain, domain.Domain);
        Assert.Equal(5, domain.Records.Count);

        var recordTypes = domain.Records.Select(r => r.RecordType).Distinct().ToList();
        Assert.Contains(DnsRecordType.CNAME, recordTypes);
        Assert.Contains(DnsRecordType.TXT, recordTypes);
        Assert.Contains(DnsRecordType.MX, recordTypes);
    }

    [Fact]
    public async Task RegisterDomainAsync_ReturnPathCname_PointsToSGrowingTarget()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainName = $"notify-{Guid.NewGuid():N}.acme.org";
        var dto = new RegisterCustomDomainDto(domainName, ReturnPathPrefix: "bounces");

        // Act
        var domain = await _service.RegisterDomainAsync(tenantId, dto);

        // Assert
        var cname = domain.Records.FirstOrDefault(r => r.RecordType == DnsRecordType.CNAME);
        Assert.NotNull(cname);
        Assert.Equal($"bounces.{domainName}", cname.Hostname);
        Assert.Equal("mail.sgrowing.net", cname.ExpectedValue);
        Assert.Equal("Return-Path CNAME", cname.Purpose);
    }

    [Fact]
    public async Task RegisterDomainAsync_SpfRecord_ContainsSGrowingIncludeDirective()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainName = $"ops-{Guid.NewGuid():N}.saas.io";

        // Act
        var domain = await _service.RegisterDomainAsync(tenantId, new RegisterCustomDomainDto(domainName));

        // Assert
        var spf = domain.Records.FirstOrDefault(r => r.Purpose == "SPF Validation");
        Assert.NotNull(spf);
        Assert.Equal(domainName, spf.Hostname);
        Assert.Equal("v=spf1 include:_spf.sgrowing.net ~all", spf.ExpectedValue);
    }

    [Fact]
    public async Task RegisterDomainAsync_DmarcRecord_DefaultsToQuarantine()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainName = $"orders-{Guid.NewGuid():N}.shop.tn";

        // Act
        var domain = await _service.RegisterDomainAsync(tenantId, new RegisterCustomDomainDto(domainName));

        // Assert
        var dmarc = domain.Records.FirstOrDefault(r => r.Purpose == "DMARC Policy");
        Assert.NotNull(dmarc);
        Assert.Equal($"_dmarc.{domainName}", dmarc.Hostname);
        Assert.Equal(DmarcPolicy.Quarantine, domain.DmarcPolicy);
        Assert.Contains("p=quarantine", dmarc.ExpectedValue);
        Assert.Contains($"rua=mailto:dmarc@{domainName}", dmarc.ExpectedValue);
    }

    [Fact]
    public async Task RegisterDomainAsync_DmarcRecord_SupportsRejectPolicy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainName = $"secure-{Guid.NewGuid():N}.bank.com";
        var dto = new RegisterCustomDomainDto(domainName, DmarcPolicy: "reject");

        // Act
        var domain = await _service.RegisterDomainAsync(tenantId, dto);

        // Assert
        Assert.Equal(DmarcPolicy.Reject, domain.DmarcPolicy);
        var dmarc = domain.Records.First(r => r.Purpose == "DMARC Policy");
        Assert.Contains("p=reject", dmarc.ExpectedValue);
    }

    [Fact]
    public async Task RegisterDomainAsync_DmarcRecord_SupportsNonePolicy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainName = $"test-{Guid.NewGuid():N}.dev.io";
        var dto = new RegisterCustomDomainDto(domainName, DmarcPolicy: "none");

        // Act
        var domain = await _service.RegisterDomainAsync(tenantId, dto);

        // Assert
        Assert.Equal(DmarcPolicy.None, domain.DmarcPolicy);
        var dmarc = domain.Records.First(r => r.Purpose == "DMARC Policy");
        Assert.Contains("p=none", dmarc.ExpectedValue);
    }

    [Fact]
    public async Task RegisterDomainAsync_DmarcRecord_UsesCustomReportEmail()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainName = $"alerts-{Guid.NewGuid():N}.hub.co";
        var dto = new RegisterCustomDomainDto(domainName, CustomDmarcReportEmail: "security-team@hub.co");

        // Act
        var domain = await _service.RegisterDomainAsync(tenantId, dto);

        // Assert
        var dmarc = domain.Records.First(r => r.Purpose == "DMARC Policy");
        Assert.Contains("rua=mailto:security-team@hub.co", dmarc.ExpectedValue);
        Assert.Equal("security-team@hub.co", domain.DmarcReportEmail);
    }

    [Fact]
    public async Task RegisterDomainAsync_InboundMxRecord_ConfiguredWithPriority10()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainName = $"bounce-{Guid.NewGuid():N}.corp.net";

        // Act
        var domain = await _service.RegisterDomainAsync(tenantId, new RegisterCustomDomainDto(domainName));

        // Assert
        var mx = domain.Records.FirstOrDefault(r => r.RecordType == DnsRecordType.MX);
        Assert.NotNull(mx);
        Assert.Equal($"mail.{domainName}", mx.Hostname);
        Assert.Equal("feedback-smtp.sgrowing.net", mx.ExpectedValue);
        Assert.Equal(10, mx.Priority);
    }

    [Fact]
    public async Task RegisterDomainAsync_InitialStatus_IsPendingDnsWithZeroHealthScore()
    {
        // Arrange
        var domainName = $"fresh-{Guid.NewGuid():N}.io";

        // Act
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto(domainName));

        // Assert
        Assert.Equal(CustomDomainStatus.PendingDns, domain.Status);
        Assert.Equal(0, domain.HealthScore);
        Assert.All(domain.Records, r => Assert.Equal(DnsRecordStatus.PropagationPending, r.Status));
        Assert.Null(domain.LastVerifiedAtUtc);
    }

    [Theory]
    [InlineData("  MAIL.Acme-Corp.COM  ", "mail.acme-corp.com")]
    [InlineData("HTTP://mail.acme-corp.com", "mail.acme-corp.com")]
    [InlineData("https://Mail.Acme-Corp.Com/", "mail.acme-corp.com")]
    [InlineData("sub.domain.tn.", "sub.domain.tn")]
    public void NormalizeDomain_CorrectlyNormalizesVariations(string raw, string expected)
    {
        string actual = CustomEmailDomainService.NormalizeDomain(raw);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizeDomain_EmptyOrWhitespace_ThrowsArgumentException(string? invalidDomain)
    {
        Assert.Throws<ArgumentException>(() => CustomEmailDomainService.NormalizeDomain(invalidDomain!));
    }

    [Theory]
    [InlineData("not-a-domain")]
    [InlineData("invalid..domain.com")]
    [InlineData(".leadingdot.com")]
    [InlineData("http:///bad")]
    public void NormalizeDomain_InvalidDomainFormat_ThrowsArgumentException(string invalidDomain)
    {
        Assert.Throws<ArgumentException>(() => CustomEmailDomainService.NormalizeDomain(invalidDomain));
    }

    [Fact]
    public async Task RegisterDomainAsync_DuplicateDomain_ThrowsInvalidOperationException()
    {
        // Arrange
        var domainName = $"dup-{Guid.NewGuid():N}.org";
        await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto(domainName));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto(domainName.ToUpperInvariant())));
    }

    [Fact]
    public async Task RegisterDomainAsync_CustomReturnPathPrefix_AppliesToCnameAndMx()
    {
        // Arrange
        var domainName = $"customprefix-{Guid.NewGuid():N}.io";
        var dto = new RegisterCustomDomainDto(domainName, ReturnPathPrefix: "pm-bounces");

        // Act
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), dto);

        // Assert
        Assert.Equal("pm-bounces", domain.ReturnPathPrefix);
        var cname = domain.Records.First(r => r.RecordType == DnsRecordType.CNAME);
        var mx = domain.Records.First(r => r.RecordType == DnsRecordType.MX);
        Assert.Equal($"pm-bounces.{domainName}", cname.Hostname);
        Assert.Equal($"pm-bounces.{domainName}", mx.Hostname);
    }

    [Fact]
    public async Task GetDomainAsync_ExistingId_ReturnsFullDomainDetail()
    {
        // Arrange
        var domainName = $"detail-{Guid.NewGuid():N}.com";
        var registered = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto(domainName));

        // Act
        var detail = await _service.GetDomainAsync(registered.Id);

        // Assert
        Assert.NotNull(detail);
        Assert.Equal(registered.Id, detail.Id);
        Assert.Equal(domainName, detail.Domain);
        Assert.NotNull(detail.Dkim);
        Assert.Equal(5, detail.Records.Count);
    }

    [Fact]
    public async Task GetDomainAsync_NonExistentId_ReturnsNull()
    {
        var result = await _service.GetDomainAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task ListDomainsAsync_FiltersByTenantId()
    {
        // Arrange
        var targetTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();

        await _service.RegisterDomainAsync(targetTenant, new RegisterCustomDomainDto($"t1-{Guid.NewGuid():N}.io"));
        await _service.RegisterDomainAsync(targetTenant, new RegisterCustomDomainDto($"t2-{Guid.NewGuid():N}.io"));
        await _service.RegisterDomainAsync(otherTenant, new RegisterCustomDomainDto($"t3-{Guid.NewGuid():N}.io"));

        // Act
        var domains = await _service.ListDomainsAsync(targetTenant);

        // Assert
        Assert.NotEmpty(domains);
        Assert.All(domains, d => Assert.Equal(targetTenant, d.TenantId));
        Assert.True(domains.Count >= 2);
    }

    [Fact]
    public async Task ListDomainsAsync_WithoutTenantId_ReturnsAllDomains()
    {
        // Act
        var domains = await _service.ListDomainsAsync(null);

        // Assert
        Assert.NotNull(domains);
        // Includes seed domains + any created
        Assert.True(domains.Count >= 4);
    }

    [Fact]
    public async Task VerifyDomainDnsAsync_SimulatePassAll_TransitionsToActiveWith100Health()
    {
        // Arrange
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"verify-pass-{Guid.NewGuid():N}.com"));

        // Act
        var result = await _service.VerifyDomainDnsAsync(domain.Id, simulateAllPass: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(CustomDomainStatus.Active, result.NewStatus);
        Assert.Equal(100, result.HealthScore);
        Assert.True(result.AllMatched);
        Assert.Equal(5, result.RecordsMatched);

        var refreshed = await _service.GetDomainAsync(domain.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(CustomDomainStatus.Active, refreshed.Status);
        Assert.Equal(100, refreshed.HealthScore);
        Assert.NotNull(refreshed.LastVerifiedAtUtc);
    }

    [Fact]
    public async Task VerifyDomainDnsAsync_SimulateFail_TransitionsToPendingOrDegraded()
    {
        // Arrange
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"verify-fail-{Guid.NewGuid():N}.com"));

        // Act
        var result = await _service.VerifyDomainDnsAsync(domain.Id, simulateAllPass: false);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.AllMatched);
        Assert.NotEqual(CustomDomainStatus.Active, result.NewStatus);
        Assert.True(result.HealthScore < 100);
    }

    [Fact]
    public async Task VerifyDomainDnsAsync_NonExistentDomain_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.VerifyDomainDnsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ExportRegistrarConfig_Cloudflare_ProducesValidBindZoneFile()
    {
        // Arrange
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"cf-{Guid.NewGuid():N}.net"));

        // Act
        var export = _service.ExportRegistrarConfig(domain.Id, RegistrarProvider.Cloudflare);

        // Assert
        Assert.NotNull(export);
        Assert.Equal("Cloudflare", export.Registrar);
        Assert.Equal("ZoneFile", export.Format);
        Assert.Contains("$ORIGIN", export.RawOutput);
        Assert.Contains("$TTL 300", export.RawOutput);
        Assert.Contains("CNAME    mail.sgrowing.net.", export.RawOutput);
        Assert.Contains("TXT    \"v=DKIM1;", export.RawOutput);
        Assert.Contains("TXT    \"v=spf1", export.RawOutput);
        Assert.Contains("TXT    \"v=DMARC1;", export.RawOutput);
        Assert.Contains("MX    10    feedback-smtp.sgrowing.net.", export.RawOutput);
    }

    [Fact]
    public async Task ExportRegistrarConfig_AwsRoute53_ProducesValidJsonChangeBatch()
    {
        // Arrange
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"aws-{Guid.NewGuid():N}.com"));

        // Act
        var export = _service.ExportRegistrarConfig(domain.Id, RegistrarProvider.AwsRoute53);

        // Assert
        Assert.NotNull(export);
        Assert.Equal("AwsRoute53", export.Registrar);
        Assert.Equal("JsonChangeBatch", export.Format);

        // Validate JSON parsing
        using var jsonDoc = JsonDocument.Parse(export.RawOutput);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("Changes", out var changes));
        Assert.Equal(5, changes.GetArrayLength());

        var firstChange = changes[0];
        Assert.Equal("UPSERT", firstChange.GetProperty("Action").GetString());
    }

    [Fact]
    public async Task ExportRegistrarConfig_GoDaddy_ProducesValidJsonApiArray()
    {
        // Arrange
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"godaddy-{Guid.NewGuid():N}.com"));

        // Act
        var export = _service.ExportRegistrarConfig(domain.Id, RegistrarProvider.GoDaddy);

        // Assert
        Assert.NotNull(export);
        Assert.Equal("GoDaddy", export.Registrar);
        Assert.Equal("JsonApi", export.Format);

        using var jsonDoc = JsonDocument.Parse(export.RawOutput);
        Assert.Equal(JsonValueKind.Array, jsonDoc.RootElement.ValueKind);
        Assert.Equal(5, jsonDoc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ExportRegistrarConfig_Namecheap_ProducesFormattedTable()
    {
        // Arrange
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"namecheap-{Guid.NewGuid():N}.com"));

        // Act
        var export = _service.ExportRegistrarConfig(domain.Id, RegistrarProvider.Namecheap);

        // Assert
        Assert.NotNull(export);
        Assert.Equal("Namecheap", export.Registrar);
        Assert.Equal("Table", export.Format);
        Assert.Contains("CNAME", export.RawOutput);
        Assert.Contains("sgrowing._domainkey", export.RawOutput);
        Assert.Contains("mail.sgrowing.net", export.RawOutput);
    }

    [Theory]
    [InlineData("cloudflare", RegistrarProvider.Cloudflare)]
    [InlineData("CLOUDFLARE", RegistrarProvider.Cloudflare)]
    [InlineData("aws", RegistrarProvider.AwsRoute53)]
    [InlineData("route53", RegistrarProvider.AwsRoute53)]
    [InlineData("awsroute53", RegistrarProvider.AwsRoute53)]
    [InlineData("godaddy", RegistrarProvider.GoDaddy)]
    [InlineData("namecheap", RegistrarProvider.Namecheap)]
    public async Task ExportRegistrarConfig_CaseInsensitiveName_ResolvesCorrectProvider(string name, RegistrarProvider expected)
    {
        // Arrange
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"reg-{Guid.NewGuid():N}.com"));

        // Act
        var export = _service.ExportRegistrarConfig(domain.Id, name);

        // Assert
        Assert.Equal(expected.ToString(), export.Registrar);
    }

    [Fact]
    public async Task ExportRegistrarConfig_UnsupportedRegistrar_ThrowsArgumentException()
    {
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"err-{Guid.NewGuid():N}.com"));

        Assert.Throws<ArgumentException>(() =>
            _service.ExportRegistrarConfig(domain.Id, "unknown-registrar-xyz"));
    }

    [Fact]
    public async Task DeleteDomainAsync_ExistingDomain_DeletesSuccessfully()
    {
        // Arrange
        var domain = await _service.RegisterDomainAsync(Guid.NewGuid(), new RegisterCustomDomainDto($"del-{Guid.NewGuid():N}.com"));

        // Act
        bool deleted = await _service.DeleteDomainAsync(domain.Id);

        // Assert
        Assert.True(deleted);
        var detail = await _service.GetDomainAsync(domain.Id);
        Assert.Null(detail);
    }

    [Fact]
    public async Task DeleteDomainAsync_NonExistentDomain_ReturnsFalse()
    {
        bool deleted = await _service.DeleteDomainAsync(Guid.NewGuid());
        Assert.False(deleted);
    }

    [Fact]
    public async Task GetOverviewMetricsAsync_CalculatesAccurateAggregates()
    {
        // Act
        var metrics = await _service.GetOverviewMetricsAsync();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.TotalDomains >= 4);
        Assert.True(metrics.ActiveDomains >= 1);
        Assert.True(metrics.PendingDnsDomains >= 1);
        Assert.True(metrics.DegradedDomains >= 1);
        Assert.True(metrics.VerifyingDomains >= 1);
        Assert.True(metrics.AverageHealthScore >= 0 && metrics.AverageHealthScore <= 100);
        Assert.Equal(metrics.TotalDomains, metrics.TotalDkimKeysGenerated);
    }

    [Fact]
    public async Task SeedData_ContainsFourDiverseTenantDomains()
    {
        // Act
        var domains = await _service.ListDomainsAsync(null);

        // Assert
        var active = domains.FirstOrDefault(d => d.Domain == "mail.acme-corp.com");
        var pending = domains.FirstOrDefault(d => d.Domain == "billing.fintech.tn");
        var degraded = domains.FirstOrDefault(d => d.Domain == "updates.growthpulse.io");
        var verifying = domains.FirstOrDefault(d => d.Domain == "outreach.saasaccelerate.com");

        Assert.NotNull(active);
        Assert.Equal(CustomDomainStatus.Active, active.Status);
        Assert.Equal(100, active.HealthScore);

        Assert.NotNull(pending);
        Assert.Equal(CustomDomainStatus.PendingDns, pending.Status);
        Assert.Equal(0, pending.HealthScore);

        Assert.NotNull(degraded);
        Assert.Equal(CustomDomainStatus.DnsDegraded, degraded.Status);
        Assert.Equal(60, degraded.HealthScore);

        Assert.NotNull(verifying);
        Assert.Equal(CustomDomainStatus.Verifying, verifying.Status);
    }

    [Fact]
    public async Task ConcurrentDomainRegistrations_ThreadSafeExecutionWithoutDataCorruption()
    {
        // Arrange
        const int concurrentCount = 15;
        var tasks = new List<Task<CustomEmailDomain>>();

        // Act
        for (int i = 0; i < concurrentCount; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                var tenantId = Guid.NewGuid();
                var domainName = $"concurrency-{index}-{Guid.NewGuid():N}.domain.com";
                return await _service.RegisterDomainAsync(tenantId, new RegisterCustomDomainDto(domainName));
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentCount, results.Length);
        Assert.Equal(concurrentCount, results.Select(r => r.Id).Distinct().Count());
        Assert.Equal(concurrentCount, results.Select(r => r.Domain).Distinct().Count());
        Assert.All(results, r => Assert.Equal(2048, r.Dkim.KeyLengthBits));
    }
}
