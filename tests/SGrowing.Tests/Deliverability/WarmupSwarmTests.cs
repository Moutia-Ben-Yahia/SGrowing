using FluentAssertions;
using SGrowing.Api.Modules.Deliverability.WarmupSwarm;
using Xunit;

namespace SGrowing.Tests.Deliverability;

public class WarmupSwarmTests
{
    private readonly WarmupSwarmEngine _engine = new(seed: 101);

    [Fact]
    public void EnrollAccount_GeneratesMailboxWithValidDnsAndInitialScore()
    {
        // Arrange
        var request = new EnrollWarmupAccountRequest(
            TenantId: "tenant_warmup_01",
            Email: "alex@enterprise-growth.io",
            SmtpHost: "smtp.mailgun.org",
            TargetDailyVolume: 50
        );

        // Act
        var account = _engine.EnrollAccount(request);

        // Assert
        account.Should().NotBeNull();
        account.TenantId.Should().Be("tenant_warmup_01");
        account.Email.Should().Be("alex@enterprise-growth.io");
        account.Status.Should().Be(WarmupStatus.WarmingUp);
        account.DnsHealth.Should().HaveCount(4);
        account.DnsHealth.Should().Contain(d => d.RecordType == "SPF" && d.IsValid);
        account.DnsHealth.Should().Contain(d => d.RecordType == "DKIM" && d.IsValid);
        account.DnsHealth.Should().Contain(d => d.RecordType == "DMARC" && d.IsValid);
    }

    [Fact]
    public void SimulateP2PExchange_IncrementsSentVolumeAndImprovesReputation()
    {
        // Arrange
        var account = _engine.EnrollAccount(new EnrollWarmupAccountRequest(
            TenantId: "tenant_warmup_02",
            Email: "sarah@outbound-ops.com",
            SmtpHost: "smtp.sendgrid.net",
            TargetDailyVolume: 30
        ));

        var initialScore = account.ReputationScore;

        // Act
        var summary = _engine.SimulateP2PExchange(new SimulateP2PExchangeRequest(
            TenantId: "tenant_warmup_02",
            AccountId: account.AccountId,
            ExchangeBatchSize: 15
        ));

        // Assert
        summary.Should().NotBeNull();
        summary.MessagesDispatched.Should().Be(15);
        summary.SimulatedOpens.Should().BeGreaterThan(10);
        summary.RescuedFromSpam.Should().BeGreaterThan(0);
        summary.NewReputationScore.Should().BeGreaterThanOrEqualTo(initialScore);

        var updatedAccount = _engine.GetAccount("tenant_warmup_02", account.AccountId);
        updatedAccount.Should().NotBeNull();
        updatedAccount!.CurrentDailySent.Should().Be(15);
    }

    [Fact]
    public void AuditDns_EvaluatesDomainDirectivesAndReturnsBadge()
    {
        // Arrange
        var account = _engine.EnrollAccount(new EnrollWarmupAccountRequest(
            TenantId: "tenant_warmup_03",
            Email: "support@hypergrowth.tech",
            SmtpHost: "smtp.postmarkapp.com",
            TargetDailyVolume: 40
        ));

        // Act
        var audited = _engine.AuditDns(new AuditDnsRequest("tenant_warmup_03", account.AccountId, "hypergrowth.tech"));

        // Assert
        audited.Should().NotBeNull();
        audited.DnsHealth.Should().HaveCount(4);
        audited.DnsHealth.All(d => d.StatusBadge == "PASS").Should().BeTrue();
        audited.Status.Should().Be(WarmupStatus.Healthy);
        audited.ReputationScore.Should().BeGreaterThanOrEqualTo(85.0);
    }
}
