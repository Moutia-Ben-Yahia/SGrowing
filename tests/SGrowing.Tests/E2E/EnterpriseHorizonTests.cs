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

    [Fact]
    public async Task MasterEnterpriseE2E_FaultInjection_ValidatesTamperedSignaturesAndInvalidContractTokens()
    {
        var tenantId = Guid.NewGuid();

        // 1. Webhook Signature Tamper Verification
        var appSecret = "secret_key_live_99";
        var validPayload = "{\"event\":\"invoice.payment_failed\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var validSig = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(validPayload))).ToLowerInvariant();

        // Valid signature passes
        Assert.True(WebhookSignatureValidator.VerifyMetaSignature(validPayload, appSecret, validSig));

        // Tampered signature fails
        var tamperedSig = validSig.Substring(0, validSig.Length - 4) + "beef";
        Assert.False(WebhookSignatureValidator.VerifyMetaSignature(validPayload, appSecret, tamperedSig));

        // Tampered payload fails
        var tamperedPayload = "{\"event\":\"invoice.payment_succeeded\"}";
        Assert.False(WebhookSignatureValidator.VerifyMetaSignature(tamperedPayload, appSecret, validSig));

        // 2. Contract Signing Invalid Token Rejection
        var contractService = new ContractSigningService(_db, NullLogger<ContractSigningService>.Instance);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            contractService.CompleteSigningAsync("nonexistent_tampered_token_999", "Intruder"));
    }

    [Fact]
    public async Task MasterEnterpriseE2E_AudioSilence_RagEmpty_And_AesDecryptionFailures()
    {
        var tenantId = Guid.NewGuid();

        // 1. Silent Audio Frame: Does not trigger barge-in interruption
        var bargeInService = new BargeInInterruptionService(NullLogger<BargeInInterruptionService>.Instance);
        var voiceHandler = new StreamingVoiceWebSocketHandler(bargeInService, NullLogger<StreamingVoiceWebSocketHandler>.Instance);
        var voiceSession = StreamingVoiceCallSession.Create(tenantId, "CA_LIVE_SILENCE", "MZ_STREAM_SILENCE");

        var silentChunk = voiceHandler.ProcessInboundAudioChunk(voiceSession, "", 1);
        Assert.False(silentChunk.IsSpeech);
        Assert.NotEqual(VadStatus.BargeInInterrupted, silentChunk.Status);
        Assert.Null(silentChunk.ClearBufferCommand);

        // 2. AES Encryption Tamper Verification
        var aes = new AesEncryptionService();
        var encrypted = aes.Encrypt("Confidential Customer Record");
        var tamperedCiphertext = Convert.ToBase64String(Encoding.UTF8.GetBytes("BadCiphertextBytes99"));
        Assert.ThrowsAny<CryptographicException>(() =>
            aes.Decrypt(tamperedCiphertext, encrypted.NonceBase64, encrypted.TagBase64));

        // 3. Contract Dispatch Failure on Nonexistent ID
        var contractService = new ContractSigningService(_db, NullLogger<ContractSigningService>.Instance);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            contractService.DispatchInChatContractAsync(tenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task MasterEnterpriseE2E_ContractViewAudit_And_StandaloneContractSigning()
    {
        var tenantId = Guid.NewGuid();
        var contractService = new ContractSigningService(_db, NullLogger<ContractSigningService>.Instance);

        // 1. Create a standalone vendor service level agreement (no CRM deal attached)
        var contract = await contractService.CreateContractAsync(
            tenantId,
            title: "Vendor Enterprise SLA Master Agreement",
            contractType: "SLA",
            signerName: "Victoria Thorne",
            signerEmail: "victoria@clouddefense.org",
            contractValue: 85000m,
            companyId: null,
            dealId: null
        );

        Assert.Equal(ContractStatus.Draft, contract.Status);
        Assert.NotNull(contract.SigningToken);

        // 2. Mark viewed when client loads electronic signing page
        var viewed = await contractService.GetAndMarkViewedAsync(contract.SigningToken);
        Assert.NotNull(viewed);
        Assert.Equal(ContractStatus.Viewed, viewed!.Status);

        // 3. Execute legal electronic signature
        var signRes = await contractService.CompleteSigningAsync(
            contract.SigningToken,
            signerSignatureName: "Victoria Thorne",
            signedPdfUrl: "https://storage.sgrowing.io/contracts/sla_signed_99.pdf"
        );

        Assert.True(signRes.Success);
        Assert.Equal(ContractStatus.Signed, signRes.Status);
        Assert.Equal(85000m, signRes.ContractValue);
        Assert.Null(signRes.ClosedWonDealId);
        Assert.Equal("https://storage.sgrowing.io/contracts/sla_signed_99.pdf", signRes.SignedPdfUrl);
    }

    [Fact]
    public async Task MasterEnterpriseE2E_WarmTransferValidation_And_MultiLingualEncryptionLifecycle()
    {
        var tenantId = Guid.NewGuid();

        // 1. Warm SIP Transfer creates valid SIP TwiML instructions
        var bargeInService = new BargeInInterruptionService(NullLogger<BargeInInterruptionService>.Instance);
        var voiceSession = StreamingVoiceCallSession.Create(tenantId, "CA_LIVE_TRANSFER_OK", "MZ_STREAM_OK");

        var validTransfer = bargeInService.InitiateWarmSipTransfer(
            voiceSession,
            "sip:tier2-escalation@sgrowing.voice.net",
            "Urgent deal closing consultation",
            "Customer agreed to enterprise subscription");
        Assert.True(validTransfer.Success);
        Assert.Equal("sip:tier2-escalation@sgrowing.voice.net", validTransfer.TargetSipUri);
        Assert.Contains("tier2-escalation", validTransfer.TwiMlXml);
        Assert.True(validTransfer.ContextHeaders.ContainsKey("X-Transfer-Reason"));

        // 2. AES Encryption & Decryption preserves multi-byte international text
        var aes = new AesEncryptionService();
        var multiLingualPayload = "اتفاقية المؤسسات لخدمات الحوسبة السحابية / エンタープライズ契約書 / Global Enterprise SLA 2026";
        var encrypted = aes.Encrypt(multiLingualPayload);
        var decrypted = aes.Decrypt(encrypted.CiphertextBase64, encrypted.NonceBase64, encrypted.TagBase64);
        Assert.Equal(multiLingualPayload, decrypted);

        // 3. Contract Signing on Nonexistent Token Throws Cleanly
        var contractService = new ContractSigningService(_db, NullLogger<ContractSigningService>.Instance);
        var nullResult = await contractService.GetAndMarkViewedAsync("invalid_token_99999");
        Assert.Null(nullResult);
    }
}
