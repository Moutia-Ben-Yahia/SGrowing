using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Deliverability;
using Xunit;

namespace SGrowing.Tests.Deliverability;

public class WarmupAndPlacementTests
{
    private readonly WarmupThreadGeneratorService _threadGenerator = new();

    private (SGrowingDbContext db, PlacementTestService service) CreatePlacementService()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SGrowingDbContext(options);
        var deliverability = new EmailDeliverabilityService(db);
        var placement = new PlacementTestService(deliverability);
        return (db, placement);
    }

    [Fact]
    public void WarmupThreadGenerator_GeneratesContextualThreadingHeaders_AndTurns()
    {
        // Arrange
        var sender = "outreach@growth-corp.com";
        var peer = "alex@partner-network.io";

        // Act
        var thread = _threadGenerator.GenerateThread(sender, peer, WarmupDialogueTopic.ProductInquiry, turnsCount: 4, seed: 123);

        // Assert
        Assert.NotNull(thread);
        Assert.Equal(WarmupDialogueTopic.ProductInquiry, thread.Topic);
        Assert.Equal(4, thread.Turns.Count);

        // Check threading headers
        var turn1 = thread.Turns[0];
        Assert.Equal(sender, turn1.SenderEmail);
        Assert.Equal(peer, turn1.RecipientEmail);
        Assert.Null(turn1.InReplyTo);

        var turn2 = thread.Turns[1];
        Assert.Equal(peer, turn2.SenderEmail);
        Assert.Equal(sender, turn2.RecipientEmail);
        Assert.StartsWith("Re: ", turn2.Subject);
        Assert.Equal(turn1.MessageId, turn2.InReplyTo);
        Assert.Contains(turn1.MessageId, turn2.References);

        var turn3 = thread.Turns[2];
        Assert.Equal(turn2.MessageId, turn3.InReplyTo);
        Assert.Contains(turn1.MessageId, turn3.References);
        Assert.Contains(turn2.MessageId, turn3.References);
    }

    [Fact]
    public void WarmupThreadGenerator_SimulatesHumanBehavior_ReadingDelayAndFlags()
    {
        // Arrange
        var sender = "agent@saas-outreach.com";
        var peer = "peer@warmup-network.org";

        // Act
        var thread = _threadGenerator.GenerateThread(sender, peer, WarmupDialogueTopic.PartnershipProposal, turnsCount: 4);

        // Assert
        Assert.All(thread.Turns, t =>
        {
            Assert.True(t.EmulatedReadingDelaySeconds >= 30);
            Assert.False(string.IsNullOrEmpty(t.Body));
        });
        Assert.Contains(thread.Turns, t => t.MarkAsImportant);
    }

    [Fact]
    public async Task PlacementTestService_CleanEmail_ReturnsHighInboxPlacementAcrossEsps()
    {
        // Arrange
        var (_, service) = CreatePlacementService();
        var tenantId = Guid.NewGuid();

        // Act
        var report = await service.RunSeedPlacementTestAsync(
            tenantId,
            senderEmail: "founder@sgrowing.io",
            subject: "Quick question regarding our product roadmap alignment",
            bodyHtml: "<p>Hi Alex, would you have 10 minutes this Thursday to sync on our integration?</p>"
        );

        // Assert
        Assert.NotNull(report);
        Assert.Equal(20, report.TotalSeedsTested); // 4 seeds * 5 ESPs
        Assert.True(report.OverallInboxPlacementPercentage >= 75.0);
        Assert.Equal(5, report.EspBreakdowns.Count);
        Assert.Contains(report.EspBreakdowns, b => b.Esp == EspCategory.GoogleWorkspace);
        Assert.Contains(report.EspBreakdowns, b => b.Esp == EspCategory.Microsoft365);
        Assert.NotEmpty(report.InboxingRecommendations);
    }

    [Fact]
    public void WarmupThreadGenerator_MalformedSenderOrPeerEmail_DoesNotThrowIndexOutOfRange()
    {
        // Arrange: Emails missing '@' symbol
        var sender = "raw-identifier-without-at";
        var peer = "peer-no-domain";

        // Act & Assert: Should not throw IndexOutOfRangeException
        var thread = _threadGenerator.GenerateThread(sender, peer, WarmupDialogueTopic.MeetingFollowUp, turnsCount: 3);

        Assert.NotNull(thread);
        Assert.Equal(3, thread.Turns.Count);
        Assert.All(thread.Turns, t =>
        {
            Assert.Contains("sgrowing-warmup@sgrowing.io", t.MessageId);
        });
    }

    [Fact]
    public async Task PlacementTestService_HighSpamRisk_DetectsSpamFolders()
    {
        // Arrange
        var (_, service) = CreatePlacementService();
        var tenantId = Guid.NewGuid();

        // High spam content: triggers SpamScore > 40
        var subject = "URGENT ACT NOW: 100% FREE MONEY GUARANTEED CASINO VIAGRA";
        var body = "WINNER WINNER CONGRATULATIONS ACT NOW 100% FREE CASINO CLAIM YOUR CASH!";

        // Act
        var report = await service.RunSeedPlacementTestAsync(tenantId, "spammer@bad.com", subject, body);

        // Assert
        Assert.NotNull(report);
        Assert.True(report.OverallSpamPercentage > 50.0);
        Assert.Contains(report.InboxingRecommendations, r => r.Contains("Deliverability alert"));
    }
}
