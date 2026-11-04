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

    [Fact]
    public async Task SlaEscalationEngine_EvaluateEscalationPolicies_WhenBreachedAndCritical_ElevatesPriorityAndSetsFlags()
    {
        // Arrange
        var (db, service) = CreateSlaContext();
        var tenantId = Guid.NewGuid();

        var policy = SlaPolicy.Create(tenantId, "Urgent Priority SLA", ConversationPriority.Normal, 20, 120);
        db.SlaPolicies.Add(policy);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        conv.Priority = ConversationPriority.Normal;
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(conv, DateTime.UtcNow.AddMinutes(-40)); // 40 min elapsed vs 20 min target => ratio 2.0 (Critical)
        conv.Status = ConversationStatus.Open;
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        // Act
        var result = await service.EvaluateEscalationPoliciesAsync(tenantId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalActiveEvaluated);
        Assert.Equal(1, result.TotalCritical);
        Assert.NotEmpty(result.Escalations);

        var escalation = result.Escalations[0];
        Assert.Equal(conv.Id, escalation.ConversationId);
        Assert.Equal(SlaEscalationTier.Critical, escalation.EscalationTier);
        Assert.True(escalation.PriorityElevated);

        var updatedConv = await db.Conversations.FindAsync(conv.Id);
        Assert.True(updatedConv!.IsSlaBreached);
        Assert.Equal(ConversationPriority.Urgent, updatedConv.Priority);
    }

    [Fact]
    public async Task CannedMacroService_CreateMacroAsync_StoresAndRetrievesCustomMacro()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new CreateCannedMacroRequest(
            TriggerKey: "vipwelcome",
            Title: "VIP Welcome Onboarding",
            Category: "Onboarding",
            ContentTemplate: "Welcome {{contact.name}} to {{tier.name}} tier of {{company.name}}!"
        );

        // Act
        var created = await _macroService.CreateMacroAsync(tenantId, request);

        // Assert
        Assert.NotNull(created);
        Assert.Equal("/vipwelcome", created.TriggerKey);
        Assert.Contains("tier.name", created.RequiredVariables);

        var context = new MacroSubstitutionContext(
            ContactName: "Sarah Connor",
            CompanyName: "Cyberdyne Systems",
            CustomVariables: new Dictionary<string, string> { ["tier.name"] = "Diamond" }
        );

        var resolved = await _macroService.ResolveMacroAsync(tenantId, "vipwelcome", context);
        Assert.Contains("Welcome Sarah Connor to Diamond tier of Cyberdyne Systems!", resolved.ResolvedContent);
        Assert.Empty(resolved.MissingVariables);
    }

    [Fact]
    public async Task CannedMacroService_ResolveMacro_WhenMissingRequiredVariables_FlagsMissingVariables()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new CreateCannedMacroRequest(
            TriggerKey: "/shipnotice",
            Title: "Shipping Notice",
            Category: "Logistics",
            ContentTemplate: "Hello {{contact.name}}, your order {{order.number}} of {{order.amount}} was shipped!"
        );
        await _macroService.CreateMacroAsync(tenantId, request);

        // Context missing order details
        var context = new MacroSubstitutionContext(ContactName: "John Doe");

        // Act
        var resolution = await _macroService.ResolveMacroAsync(tenantId, "/shipnotice", context);

        // Assert
        Assert.NotNull(resolution);
        Assert.Contains("order.number", resolution.MissingVariables);
        Assert.Contains("order.amount", resolution.MissingVariables);
    }

    [Fact]
    public void MimeEmailParser_ParsesSimplePlainTextEmail_WithoutAttachments()
    {
        // Arrange
        var rawMime = """
            From: "Bob Support" <bob@vendor.org>
            To: "Inbound" <inbound@sgrowing.io>
            Subject: Simple Inquiry
            Date: Tue, 27 Oct 2026 09:15:00 +0000
            Message-ID: <simple-inq-123@vendor.org>
            Content-Type: text/plain; charset=utf-8

            Just wanted to check if your API documentation is publicly available.
            """;

        // Act
        var parsed = _parser.ParseRawMime(rawMime);

        // Assert
        Assert.Equal("Simple Inquiry", parsed.Subject);
        Assert.Equal("\"Bob Support\" <bob@vendor.org>", parsed.From);
        Assert.Contains("Just wanted to check if your API documentation is publicly available.", parsed.CleanTextBody);
        Assert.Empty(parsed.Attachments);
    }
}
