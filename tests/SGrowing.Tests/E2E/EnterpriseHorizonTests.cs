using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Api.Modules.Security;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.E2E;

public class EnterpriseHorizonTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SGrowingDbContext _db;

    public EnterpriseHorizonTests()
    {
        var services = new ServiceCollection();
        var dbName = "EnterpriseHorizonDb_" + Guid.NewGuid();
        services.AddDbContext<SGrowingDbContext>(options => options.UseInMemoryDatabase(dbName));

        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<SGrowingDbContext>();
    }

    [Fact]
    public async Task MasterEnterpriseE2E_AllFiveHorizons_ExecuteHarmoniously()
    {
        var tenantId = Guid.NewGuid();

        // -------------------------------------------------------------
        // Horizon 1: Real-Time Audio Voice Pipeline & Barge-In
        // -------------------------------------------------------------
        var bargeInService = new BargeInInterruptionService(NullLogger<BargeInInterruptionService>.Instance);
        var voiceHandler = new StreamingVoiceWebSocketHandler(bargeInService, NullLogger<StreamingVoiceWebSocketHandler>.Instance);

        var voiceSession = StreamingVoiceCallSession.Create(tenantId, "CA_LIVE_STREAM_01", "MZ_STREAM_01");
        voiceSession.SetAgentSpeaking(true);

        // Simulate incoming speech frame (mu-law loud audio)
        byte[] loudSpeech = new byte[160];
        Array.Fill(loudSpeech, (byte)0x00);
        var chunkResult = voiceHandler.ProcessInboundAudioChunk(voiceSession, Convert.ToBase64String(loudSpeech), 1);

        Assert.Equal(VadStatus.BargeInInterrupted, chunkResult.Status);
        Assert.NotNull(chunkResult.ClearBufferCommand);
        Assert.Contains("clear", chunkResult.ClearBufferCommand);

        // Initiate Warm Transfer
        var transferResult = bargeInService.InitiateWarmSipTransfer(
            voiceSession,
            "sip:enterprise-support@sgrowing.voice.net",
            "Urgent customer contract consultation",
            "Customer wants to review enterprise SLA pricing.");

        Assert.True(transferResult.Success);
        Assert.Contains("<Dial>", transferResult.TwiMlXml);
        Assert.Contains("X-Session-Id", transferResult.TwiMlXml);

        // -------------------------------------------------------------
        // Horizon 2: Channel OAuth2 Vault & Webhook Signatures
        // -------------------------------------------------------------
        var aesVault = new AesEncryptionService();
        var rawMetaPayload = "{\"entry\":[{\"changes\":[{\"field\":\"messages\",\"value\":{\"messages\":[{\"text\":{\"body\":\"Hi!\"}}]}}]}]}";
        var appSecret = "meta_enterprise_app_secret_2026";

        var encryptedSecret = aesVault.Encrypt(appSecret);
        var vaultRecord = ChannelCredential.Create(
            tenantId,
            "MetaGraph",
            encryptedAccessToken: "enc_token_val",
            encryptedAppSecret: encryptedSecret.CiphertextBase64,
            nonceBase64: encryptedSecret.NonceBase64,
            tagBase64: encryptedSecret.TagBase64,
            expiresAtUtc: DateTime.UtcNow.AddDays(90));

        await _db.ChannelCredentials.AddAsync(vaultRecord);
        await _db.SaveChangesAsync();

        // Verify HMAC-SHA256
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var metaSig = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawMetaPayload))).ToLowerInvariant();
        Assert.True(WebhookSignatureValidator.VerifyMetaSignature(rawMetaPayload, appSecret, metaSig));

        // -------------------------------------------------------------
        // Horizon 3: True Vector RAG with Document Chunking & RRF
        // -------------------------------------------------------------
        var chunkingService = new DocumentChunkingService();
        var ragService = new VectorRagRetrievalService(_db, chunkingService);

        var docId = Guid.NewGuid();
        var docContent = "SGrowing Omnicommunication empowers support, marketing, sales, and AI voice softphones with 99.99% uptime and DocuSeal-grade electronic signatures for immediate deal execution.";
        var chunks = await ragService.IndexDocumentAsync(tenantId, docId, "SGrowing Omnicommunication Overview", docContent, "https://docs.sgrowing.com/overview");
        Assert.NotEmpty(chunks);

        var searchHits = await ragService.HybridSearchAsync(tenantId, "How does SGrowing handle electronic signatures and voice?", topK: 2);
        Assert.NotEmpty(searchHits);
        Assert.Equal("SGrowing Omnicommunication Overview", searchHits[0].DocumentTitle);
        Assert.True(searchHits[0].CombinedRrfScore > 0);

        // -------------------------------------------------------------
        // Horizon 4: Production Worker Daemon (Outbox & SLA Watchdog)
        // -------------------------------------------------------------
        var inboxId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var slaPolicy = SlaPolicy.Create(tenantId, "VIP Enterprise SLA", ConversationPriority.High, targetFirstResponseMinutes: 10, targetResolutionMinutes: 60);
        await _db.SlaPolicies.AddAsync(slaPolicy);

        var overdueConversation = Conversation.Create(tenantId, inboxId, contactId);
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(overdueConversation, DateTime.UtcNow.AddMinutes(-20));
        await _db.Conversations.AddAsync(overdueConversation);
        await _db.SaveChangesAsync();

        var watchdog = new SlaWatchdogDaemon(_serviceProvider, NullLogger<SlaWatchdogDaemon>.Instance);
        var breaches = await watchdog.CheckSlaBreachesAsync();
        Assert.True(breaches >= 1);

        var outboxDaemon = new OutboxProcessorDaemon(_serviceProvider, NullLogger<OutboxProcessorDaemon>.Instance);
        var processedOutbox = await outboxDaemon.ProcessBatchAsync();
        Assert.True(processedOutbox >= 1);

        // -------------------------------------------------------------
        // Horizon 5: B2B Lead Enrichment & In-Chat E-Signatures
        // -------------------------------------------------------------
        var enrichmentService = new LeadEnrichmentService(_db, NullLogger<LeadEnrichmentService>.Instance);
        var contractService = new ContractSigningService(_db, NullLogger<ContractSigningService>.Instance);

        var enrichedProfile = await enrichmentService.EnrichLeadAsync(tenantId, "marcus@salesforce.com");
        Assert.Equal("salesforce.com", enrichedProfile.Domain);
        Assert.NotEmpty(enrichedProfile.Industry);

        var deal = Deal.Create(tenantId, contactId, "Salesforce CRM Enterprise Connect", 120000m, DealStage.Proposal);
        await _db.Deals.AddAsync(deal);
        await _db.SaveChangesAsync();

        var contract = await contractService.CreateContractAsync(
            tenantId,
            "Master Services Agreement - 500 Seats",
            "MSA",
            "Marcus Sterling",
            "marcus@salesforce.com",
            120000m,
            dealId: deal.Id);

        var dispatch = await contractService.DispatchInChatContractAsync(tenantId, contract.Id);
        Assert.Contains(contract.SigningToken, dispatch.SigningUrl);

        var signResult = await contractService.CompleteSigningAsync(contract.SigningToken, "Marcus Sterling");
        Assert.True(signResult.Success);
        Assert.Equal(ContractStatus.Signed, signResult.Status);

        // Deal is automatically closed WON!
        using var verifyScope = _serviceProvider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
        var closedDeal = await verifyDb.Deals.FindAsync(deal.Id);
        Assert.Equal(DealStage.Won, closedDeal!.Stage);
    }
}
