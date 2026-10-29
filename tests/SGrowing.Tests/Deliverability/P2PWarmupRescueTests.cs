using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Deliverability;
using Xunit;

namespace SGrowing.Tests.Deliverability;

public class P2PWarmupRescueTests
{
    private static (SGrowingDbContext Db, P2PWarmupRescueService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SGrowingDbContext(options);
        var service = new P2PWarmupRescueService(db);
        return (db, service);
    }

    [Fact]
    public async Task MailboxEnrollment_CalculatesRampSchedule_AndCapEnforcement()
    {
        // Arrange
        var (_, service) = CreateService();
        var request = new EnrollMailboxRequest(
            TenantId: Guid.NewGuid(),
            Email: "alex.hunter@velocity-growth.com",
            Provider: "GoogleWorkspace",
            DailyVolumeCap: 30,
            StartingDay: 1
        );

        // Act
        var enrolled = await service.EnrollMailboxAsync(request);

        // Assert
        Assert.NotNull(enrolled);
        Assert.Equal("alex.hunter@velocity-growth.com", enrolled.Email);
        Assert.Equal("velocity-growth.com", enrolled.Domain);
        Assert.Equal(WarmupMailboxProvider.GoogleWorkspace, enrolled.Provider);
        Assert.Equal(30, enrolled.DailyVolumeCap);
        Assert.Equal(1, enrolled.CurrentRampDay);
        Assert.Equal(5, enrolled.RecommendedVolumeToday); // Day 1 base volume
        Assert.Equal(WarmupMailboxStatus.WarmingUp, enrolled.Status);

        // Test Ramp Schedule Progression
        Assert.Equal(5, service.CalculateRecommendedDailyVolume(1, 30));
        Assert.Equal(8, service.CalculateRecommendedDailyVolume(2, 30));
        Assert.Equal(11, service.CalculateRecommendedDailyVolume(3, 30));
        Assert.Equal(17, service.CalculateRecommendedDailyVolume(5, 30));
        Assert.Equal(21, service.CalculateRecommendedDailyVolume(6, 30));
        Assert.Equal(25, service.CalculateRecommendedDailyVolume(7, 30));
        Assert.Equal(30, service.CalculateRecommendedDailyVolume(12, 30)); // Capped at 30

        var projection = service.GetVolumeRampProjection(30, totalDays: 10);
        Assert.Equal(10, projection.Count);
        Assert.True(projection[9].Volume <= 30);
    }

    [Fact]
    public void CryptographicWarmupToken_GeneratesAndVerifies_TamperResistantHeader()
    {
        // Arrange
        var (_, service) = CreateService();
        var mailboxId = Guid.NewGuid();
        var exchangeId = Guid.NewGuid();
        var sender = "sender@domain-a.com";
        var peer = "peer@domain-b.com";
        var now = DateTime.UtcNow;

        // Act
        var token = service.GenerateWarmupToken(mailboxId, exchangeId, sender, peer, now);

        // Assert
        Assert.NotNull(token);
        Assert.Contains(".", token);

        bool valid = service.VerifyWarmupToken(token, out var payload);
        Assert.True(valid);
        Assert.NotNull(payload);
        Assert.Equal(mailboxId, payload.MailboxId);
        Assert.Equal(exchangeId, payload.ExchangeId);
        Assert.Equal(sender, payload.SenderEmail);
        Assert.Equal(peer, payload.RecipientEmail);

        // Verify tampering breaks token
        var tamperedToken = token[..^4] + "ABCD";
        bool tamperedValid = service.VerifyWarmupToken(tamperedToken, out _);
        Assert.False(tamperedValid);
    }

    [Fact]
    public async Task SyntheticThreadGenerator_CreatesMultiTurnDialogue_WithHeaderAndSignatures()
    {
        // Arrange
        var (_, service) = CreateService();
        var request = new TriggerExchangeRequest(null, null, "logistics");

        // Act
        var exchange = await service.TriggerWarmupExchangeAsync(request);

        // Assert
        Assert.NotNull(exchange);
        Assert.NotEmpty(exchange.Subject);
        Assert.Contains("Logistics Fleet", exchange.Subject);
        Assert.NotEmpty(exchange.SenderEmail);
        Assert.NotEmpty(exchange.PeerEmail);
        Assert.NotEqual(exchange.SenderEmail, exchange.PeerEmail);

        // Verify warmup token header
        Assert.NotNull(exchange.WarmupToken);
        Assert.True(service.VerifyWarmupToken(exchange.WarmupToken, out _));

        // Verify turns structure
        Assert.True(exchange.Turns.Count >= 3);
        var turn1 = exchange.Turns[0];
        Assert.Equal(1, turn1.TurnIndex);
        Assert.Equal(exchange.SenderEmail, turn1.SenderEmail);
        Assert.Equal(exchange.PeerEmail, turn1.RecipientEmail);
        Assert.Contains("Western freight routes", turn1.Body);
        Assert.Contains("Director of Operations", turn1.Signature);

        var turn2 = exchange.Turns[1];
        Assert.Equal(2, turn2.TurnIndex);
        Assert.Equal(exchange.PeerEmail, turn2.SenderEmail);
        Assert.Equal(exchange.SenderEmail, turn2.RecipientEmail);
        Assert.Equal(turn1.MessageId, turn2.InReplyTo);
        Assert.Contains("Solution Architect", turn2.Signature);
    }

    [Fact]
    public async Task AutonomousSpamRescueProtocol_ExecutesStateTransitions_AndTrainsClassifier()
    {
        // Arrange
        var (_, service) = CreateService();
        // Ensure a mailbox with SpamAlert is present
        await service.EnrollMailboxAsync(new EnrollMailboxRequest(
            TenantId: Guid.NewGuid(),
            Email: "rescue-test@alert-domain.com",
            Provider: "Microsoft365",
            DailyVolumeCap: 40,
            StartingDay: 5
        ));

        // Act
        var scanResult = await service.ExecuteSpamRescueScanAsync();

        // Assert
        Assert.NotNull(scanResult);
        Assert.True(scanResult.ScannedMailboxesCount > 0);
        Assert.True(scanResult.AchievedReplyRatio >= 0.35 && scanResult.AchievedReplyRatio <= 0.45);
        Assert.Contains("Google Priority Inbox", scanResult.EspClassifierImpact);

        var logs = service.GetRecentRescueLogs();
        Assert.NotEmpty(logs);

        // Check rescue state machine fields
        var sampleRescue = logs.First();
        Assert.True(sampleRescue.MovedToInbox, "Message must be moved to primary inbox");
        Assert.True(sampleRescue.FlaggedNotSpam, "Message must be flagged as Not Spam");
        Assert.True(sampleRescue.MarkedImportant, "Message must be marked Important");
        Assert.True(sampleRescue.Starred, "Message must be Starred");
        Assert.True(sampleRescue.HumanReadingDelaySeconds >= 30, "Human reading delay simulated");
        Assert.True(sampleRescue.PositiveReplySent, "Positive contextual reply sent to maintain ratio");
        Assert.NotEmpty(sampleRescue.EspMlClassifierSignal);
    }

    [Fact]
    public void DnsDiagnostics_EvaluatesSpfSyntax_AndLookupLimit()
    {
        // Arrange
        var (_, service) = CreateService();

        // Test 1: Valid compliant SPF with -all
        var validSpf = service.EvaluateSpf("corp.com", "v=spf1 include:_spf.google.com include:mailgun.org -all");
        Assert.True(validSpf.HasSpf);
        Assert.True(validSpf.SyntaxValid);
        Assert.Equal(2, validSpf.DnsLookupCount);
        Assert.False(validSpf.ExceedsLookupLimit);
        Assert.Equal("-all", validSpf.Qualifier);
        Assert.Equal("Pass", validSpf.Status);

        // Test 2: SPF exceeding 10 lookups RFC 7208 limit
        string excessiveSpf = "v=spf1 include:a.com include:b.com include:c.com include:d.com include:e.com include:f.com include:g.com include:h.com include:i.com include:j.com include:k.com -all";
        var failedSpf = service.EvaluateSpf("corp.com", excessiveSpf);
        Assert.True(failedSpf.ExceedsLookupLimit);
        Assert.Equal(11, failedSpf.DnsLookupCount);
        Assert.Equal("Fail", failedSpf.Status);
        Assert.Contains("exceeds the RFC 7208 10-DNS-lookup limit", failedSpf.Details);

        // Test 3: Missing SPF
        var missingSpf = service.EvaluateSpf("corp.com", "");
        Assert.False(missingSpf.HasSpf);
        Assert.Equal("Fail", missingSpf.Status);
    }

    [Fact]
    public void DnsDiagnostics_EvaluatesDkimKeyLength_AndSelector()
    {
        // Arrange
        var (_, service) = CreateService();

        // Test 1: 2048-bit DKIM Key
        var dkim2048 = service.EvaluateDkim("corp.com", "s1");
        Assert.True(dkim2048.HasDkim);
        Assert.Equal(2048, dkim2048.KeyLengthBits);
        Assert.True(dkim2048.IsRecommendedLength);
        Assert.Equal("Pass", dkim2048.Status);

        // Test 2: 1024-bit DKIM Key (deprecated)
        string dkim1024Record = "v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQC3QVRnQbWYY4Z0rI1X7K2z6n1l9yWqK4aB6e8D9c0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0IDAQAB";
        var dkim1024 = service.EvaluateDkim("corp.com", "default", dkim1024Record);
        Assert.True(dkim1024.HasDkim);
        Assert.Equal(1024, dkim1024.KeyLengthBits);
        Assert.False(dkim1024.IsRecommendedLength);
        Assert.Equal("Warning", dkim1024.Status);
        Assert.Contains("recommend upgrading to 2048-bit", dkim1024.Details);

        // Test 3: Missing DKIM
        var missingDkim = service.EvaluateDkim("corp.com", "default", "");
        Assert.False(missingDkim.HasDkim);
        Assert.Equal("Fail", missingDkim.Status);
    }

    [Fact]
    public void DnsDiagnostics_EvaluatesDmarcPolicy_AndBimiRecords()
    {
        // Arrange
        var (_, service) = CreateService();

        // Test 1: Strict DMARC p=reject
        var dmarcReject = service.EvaluateDmarc("corp.com", "v=DMARC1; p=reject; pct=100; rua=mailto:dmarc@corp.com; ruf=mailto:dmarc@corp.com");
        Assert.True(dmarcReject.HasDmarc);
        Assert.Equal("reject", dmarcReject.Policy);
        Assert.Equal(100, dmarcReject.Percentage);
        Assert.True(dmarcReject.HasRua);
        Assert.True(dmarcReject.HasRuf);
        Assert.Equal("Pass", dmarcReject.Status);

        // Test 2: Permissive DMARC p=none
        var dmarcNone = service.EvaluateDmarc("corp.com", "v=DMARC1; p=none; pct=100");
        Assert.Equal("none", dmarcNone.Policy);
        Assert.Equal("Warning", dmarcNone.Status);

        // Test 3: Compliant BIMI with VMC Certificate
        var bimiValid = service.EvaluateBimi("corp.com", "v=BIMI1; l=https://corp.com/brand/logo.svg; a=https://corp.com/brand/vmc.pem");
        Assert.True(bimiValid.HasBimi);
        Assert.True(bimiValid.HasValidSvgFormat);
        Assert.True(bimiValid.HasVmcCertificate);
        Assert.Equal("Pass", bimiValid.Status);

        // Test 4: BIMI without VMC Certificate
        var bimiNoVmc = service.EvaluateBimi("corp.com", "v=BIMI1; l=https://corp.com/brand/logo.svg");
        Assert.True(bimiNoVmc.HasValidSvgFormat);
        Assert.False(bimiNoVmc.HasVmcCertificate);
        Assert.Equal("Warning", bimiNoVmc.Status);
    }

    [Fact]
    public void DnsblSimulator_Checks25MajorBlacklists_AndReportsLatency()
    {
        // Arrange
        var (_, service) = CreateService();

        // Act
        var blacklists = service.CheckBlacklists("growth-scale.com");

        // Assert
        Assert.NotNull(blacklists);
        Assert.True(blacklists.Count >= 25, $"Expected at least 25 blacklists, found {blacklists.Count}");

        // Verify key blacklists are included
        Assert.Contains(blacklists, b => b.Host == "zen.spamhaus.org");
        Assert.Contains(blacklists, b => b.Host == "b.barracudacentral.org");
        Assert.Contains(blacklists, b => b.Host == "dnsbl.sorbs.net");
        Assert.Contains(blacklists, b => b.Host == "bl.spamcop.net");
        Assert.Contains(blacklists, b => b.Host == "dnsbl-1.uceprotect.net");

        foreach (var b in blacklists)
        {
            Assert.NotEmpty(b.Name);
            Assert.NotEmpty(b.Host);
            Assert.True(b.ResponseTimeMs > 0, "Response latency must be measured");
            Assert.NotEmpty(b.RemediationUrl);
        }
    }

    [Fact]
    public void DeliverabilityHealthScore_CombinesFactors_AndGeneratesActionableRemediation()
    {
        // Arrange
        var (_, service) = CreateService();
        var spfPass = service.EvaluateSpf("corp.com", "v=spf1 include:_spf.google.com -all");
        var dkimPass = service.EvaluateDkim("corp.com");
        var dmarcPass = service.EvaluateDmarc("corp.com");
        var bimiPass = service.EvaluateBimi("corp.com");
        var blacklists = service.CheckBlacklists("corp.com");

        // Act 1: Perfect configuration
        var perfectScore = service.CalculateDeliverabilityHealthScore(
            spfPass, dkimPass, dmarcPass, bimiPass, blacklists, replyRate: 0.40, rampDay: 14);

        // Assert 1
        Assert.True(perfectScore.Score >= 95, $"Expected >= 95, got {perfectScore.Score}");
        Assert.Equal("A+", perfectScore.Grade);
        Assert.Equal("Optimal", perfectScore.Status);
        Assert.Empty(perfectScore.Recommendations);

        // Act 2: Degraded configuration (weak SPF, 1024-bit DKIM, DMARC none, 1 Blacklist listing)
        var spfWeak = service.EvaluateSpf("bad.com", "v=spf1 +all");
        var dkimWeak = service.EvaluateDkim("bad.com", "default", "v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQC3QVRnQbWYY4Z0rI1X7K2z6n1l9yWqK4aB6e8D9c0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0IDAQAB");
        var dmarcWeak = service.EvaluateDmarc("bad.com", "v=DMARC1; p=none");
        var bimiNone = service.EvaluateBimi("bad.com", "");
        var blacklistsListed = service.CheckBlacklists("bad.com", simulateListings: true);

        var degradedScore = service.CalculateDeliverabilityHealthScore(
            spfWeak, dkimWeak, dmarcWeak, bimiNone, blacklistsListed, replyRate: 0.15, rampDay: 2);

        // Assert 2
        Assert.True(degradedScore.Score < 60, $"Expected < 60, got {degradedScore.Score}");
        Assert.NotEmpty(degradedScore.Recommendations);
        Assert.Contains(degradedScore.Recommendations, r => r.Contains("2048-bit"));
        Assert.Contains(degradedScore.Recommendations, r => r.Contains("quarantine") || r.Contains("reject"));
    }

    [Fact]
    public async Task AuditDomainDnsAndBlacklists_ReturnsAggregatedReport()
    {
        // Arrange
        var (_, service) = CreateService();

        // Act
        var audit = await service.AuditDomainDnsAndBlacklistsAsync("sgrowing.io");

        // Assert
        Assert.NotNull(audit);
        Assert.Equal("sgrowing.io", audit.Domain);
        Assert.True(audit.Spf.HasSpf);
        Assert.True(audit.Dkim.HasDkim);
        Assert.True(audit.Dmarc.HasDmarc);
        Assert.True(audit.TotalBlacklistsChecked >= 25);
        Assert.True(audit.OverallHealthScore.Score > 0);
    }
}
