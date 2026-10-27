using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Conversations.Macros;
using SGrowing.Api.Modules.Conversations.Mime;
using SGrowing.Api.Modules.Conversations.Sla;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Conversations;

public class OmnichannelMimeAndMacroTests
{
    private readonly MimeEmailParserService _parser = new();
    private readonly CannedMacroService _macroService = new();

    private (SGrowingDbContext db, SlaEscalationEngineService service) CreateSlaContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SGrowingDbContext(options);
        var service = new SlaEscalationEngineService(db);
        return (db, service);
    }

    [Fact]
    public void MimeEmailParser_ParsesMultipartAlternative_ExtractsTextAndAttachments()
    {
        // Arrange: RFC 822 MIME raw content
        var rawMime = """
            From: "Alice Smith" <alice@clientcorp.com>
            To: "Support" <support@sgrowing.io>
            Subject: Issue with Order #ORD-88192
            Date: Mon, 26 Oct 2026 14:32:00 +0000
            Message-ID: <msg-998811@clientcorp.com>
            Content-Type: multipart/mixed; boundary="BOUNDARY_MIXED"

            --BOUNDARY_MIXED
            Content-Type: text/plain; charset=utf-8

            Hi team,

            Could you check why my order hasn't shipped yet?

            On Mon, Oct 26, 2026 at 10:00 AM Support <support@sgrowing.io> wrote:
            > We are processing your request.

            -- 
            Best regards,
            Alice Smith
            --BOUNDARY_MIXED
            Content-Type: application/pdf
            Content-Disposition: attachment; filename="receipt.pdf"
            Content-ID: <receipt-part-1>

            JVBERi0xLjQKJcTl8uXr...
            --BOUNDARY_MIXED--
            """;

        // Act
        var parsed = _parser.ParseRawMime(rawMime);

        // Assert
        Assert.Equal("Issue with Order #ORD-88192", parsed.Subject);
        Assert.Equal("\"Alice Smith\" <alice@clientcorp.com>", parsed.From);
        Assert.Equal("<msg-998811@clientcorp.com>", parsed.MessageId);
        Assert.Single(parsed.Attachments);
        Assert.Equal("receipt.pdf", parsed.Attachments[0].FileName);
        Assert.Equal("receipt-part-1", parsed.Attachments[0].ContentId);

        // Cleaned body should strip quoted "On ... wrote:" and signature
        Assert.Contains("Could you check why my order hasn't shipped yet?", parsed.CleanTextBody);
        Assert.DoesNotContain("We are processing your request", parsed.CleanTextBody);
        Assert.DoesNotContain("Alice Smith", parsed.CleanTextBody);
    }

    [Fact]
    public async Task CannedMacroService_ResolvesHotkey_WithDynamicVariableSubstitution()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = new MacroSubstitutionContext(
            ContactName: "Karim Ben Ali",
            OrderNumber: "ORD-99120",
            OrderAmount: 145.50m,
            Currency: "TND",
            AgentName: "Sonia (Support)"
        );

        // Act
        var resolution = await _macroService.ResolveMacroAsync(tenantId, "/refund", context);

        // Assert
        Assert.NotNull(resolution);
        Assert.Contains("Karim Ben Ali", resolution.ResolvedContent);
        Assert.Contains("ORD-99120", resolution.ResolvedContent);
        Assert.Contains("145.50 TND", resolution.ResolvedContent);
        Assert.Contains("Sonia (Support)", resolution.ResolvedContent);
        Assert.Empty(resolution.MissingVariables);
    }

    [Fact]
    public async Task CannedMacroService_SearchMacrosByHotkey_ReturnsMatches()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var results = await _macroService.SearchMacrosByHotkeyAsync(tenantId, "pric");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, m => m.TriggerKey == "/pricing");
    }

    [Fact]
    public async Task SlaEscalationEngine_TriggersCsatSurvey_WithTokenizedUrl()
    {
        // Arrange
        var (db, service) = CreateSlaContext();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        // Act
        var csat = await service.TriggerCsatSurveyAsync(tenantId, conv.Id, channel: "email");

        // Assert
        Assert.NotNull(csat);
        Assert.Equal(conv.Id, csat.ConversationId);
        Assert.Contains("feedback.sgrowing.io/csat/", csat.SurveyUrl);
        Assert.Contains(conv.Id.ToString(), csat.SurveyUrl);

        // Verify outbox message saved
        var outboxMsg = await db.OutboxMessages.FirstOrDefaultAsync(o => o.TenantId == tenantId);
        Assert.NotNull(outboxMsg);
        Assert.Equal("CsatSurveyTriggeredEvent", outboxMsg.EventType);
    }
}
