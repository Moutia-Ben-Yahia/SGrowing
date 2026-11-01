using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Security.Vault;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Security;

public class ComplianceVaultAndGdprCascadeTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tenantIdB = Guid.NewGuid();

    private SGrowingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    #region 1. Merkle Compliance Audit Ledger Tests

    [Fact]
    public async Task AppendAuditBlock_GenesisBlock_LinksToGenesisHash()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();

        // Act
        var block = await ledger.AppendAuditBlockAsync(
            _tenantId,
            actor: "admin@sgrowing.io",
            action: "SECURITY_POLICY_UPDATE",
            resource: "Policy/SOC2-CC6.1");

        // Assert
        block.Should().NotBeNull();
        block.Index.Should().Be(0);
        block.PreviousBlockHash.Should().Be(MerkleComplianceAuditLedgerService.GenesisHash);
        block.BlockHash.Should().NotBeNullOrWhiteSpace();
        block.BlockHash.Length.Should().Be(64);
    }

    [Fact]
    public async Task AppendAuditBlock_SequentialChaining_LinksPreviousHashCorrectly()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();

        // Act
        var block0 = await ledger.AppendAuditBlockAsync(_tenantId, "alice@sgrowing.io", "CREATE_USER", "User/1");
        var block1 = await ledger.AppendAuditBlockAsync(_tenantId, "bob@sgrowing.io", "ASSIGN_ROLE", "Role/Admin");
        var block2 = await ledger.AppendAuditBlockAsync(_tenantId, "carol@sgrowing.io", "REVOKE_KEY", "ApiKey/Test");

        // Assert
        block1.Index.Should().Be(1);
        block1.PreviousBlockHash.Should().Be(block0.BlockHash);

        block2.Index.Should().Be(2);
        block2.PreviousBlockHash.Should().Be(block1.BlockHash);

        var blocks = await ledger.GetBlocksAsync(_tenantId);
        blocks.Should().HaveCount(3);
    }

    [Fact]
    public async Task AppendAuditBlock_CalculatesDeterministicSha256Formula()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        var timestamp = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var block = await ledger.AppendAuditBlockAsync(
            _tenantId,
            actor: "sec-bot@sgrowing.io",
            action: "ENCLAVE_INITIALIZATION",
            resource: "Vault/HSM-01",
            metadataJson: "{\"enclave\":\"nitro\"}",
            timestampUtc: timestamp);

        // Compute expected using exact formula: SHA256(Hi-1 || EventId || TenantId || Actor || Action || Resource || TimestampUtc)
        var expectedInput = $"{MerkleComplianceAuditLedgerService.GenesisHash}|{block.EventId:D}|{_tenantId:D}|sec-bot@sgrowing.io|ENCLAVE_INITIALIZATION|Vault/HSM-01|{timestamp:O}";
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedInput))).ToLowerInvariant();

        // Assert
        block.BlockHash.Should().Be(expectedHash);
    }

    [Fact]
    public async Task VerifyLedgerIntegrity_WhenUntampered_ReturnsValid()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        await ledger.AppendAuditBlockAsync(_tenantId, "alice", "LOGIN", "Auth/Session");
        await ledger.AppendAuditBlockAsync(_tenantId, "bob", "EXPORT_REPORT", "Analytics/LTV");
        await ledger.AppendAuditBlockAsync(_tenantId, "carol", "SIGN_CONTRACT", "Contract/MSA-99");

        // Act
        var result = await ledger.VerifyLedgerIntegrityAsync(_tenantId);

        // Assert
        result.IsValid.Should().BeTrue();
        result.TotalBlocksVerified.Should().Be(3);
        result.Violations.Should().BeEmpty();
        result.CurrentMerkleRoot.Should().NotBe(MerkleComplianceAuditLedgerService.GenesisHash);
    }

    [Fact]
    public async Task VerifyLedgerIntegrity_WhenActorTampered_DetectsTampering()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        var b0 = await ledger.AppendAuditBlockAsync(_tenantId, "alice", "LOGIN", "Auth/Session");
        var b1 = await ledger.AppendAuditBlockAsync(_tenantId, "bob", "EXPORT_REPORT", "Analytics/LTV");

        // Act - Tamper the actor in block 1
        await ledger.TamperBlockForTestingAsync(_tenantId, b1.EventId, tamperedActor: "MALICIOUS_INTRUDER");
        var result = await ledger.VerifyLedgerIntegrityAsync(_tenantId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().NotBeEmpty();
        result.Violations.Should().Contain(v => v.Contains("Tampered block detected") && v.Contains(b1.EventId.ToString()));
    }

    [Fact]
    public async Task VerifyLedgerIntegrity_WhenActionTampered_DetectsTampering()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        var b0 = await ledger.AppendAuditBlockAsync(_tenantId, "alice", "READ_FILE", "Docs/Confidential");

        // Act - Tamper action
        await ledger.TamperBlockForTestingAsync(_tenantId, b0.EventId, tamperedAction: "DELETE_ALL_RECORDS");
        var result = await ledger.VerifyLedgerIntegrityAsync(_tenantId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("Tampered block detected"));
    }

    [Fact]
    public async Task VerifyLedgerIntegrity_WhenResourceTampered_DetectsTampering()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        var b0 = await ledger.AppendAuditBlockAsync(_tenantId, "alice", "EXPORT", "Resource/Public");

        // Act - Tamper resource
        await ledger.TamperBlockForTestingAsync(_tenantId, b0.EventId, tamperedResource: "Resource/TopSecretFinancials");
        var result = await ledger.VerifyLedgerIntegrityAsync(_tenantId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("Tampered block detected"));
    }

    [Fact]
    public async Task GenerateEpochMerkleRoot_ProducesDeterministicBalancedPairwiseRoot()
    {
        // Arrange
        var ledger1 = new MerkleComplianceAuditLedgerService();
        var ledger2 = new MerkleComplianceAuditLedgerService();
        var fixedTime = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        var b1 = await ledger1.AppendAuditBlockAsync(_tenantId, "user1", "ACTION_A", "RES_1", timestampUtc: fixedTime);
        var b2 = await ledger1.AppendAuditBlockAsync(_tenantId, "user2", "ACTION_B", "RES_2", timestampUtc: fixedTime.AddMinutes(1));

        // Act
        var epoch1 = await ledger1.GenerateEpochMerkleRootAsync(_tenantId);
        var epoch2 = await ledger1.GenerateEpochMerkleRootAsync(_tenantId);

        // Assert
        epoch1.MerkleRoot.Should().NotBeNullOrWhiteSpace();
        epoch1.MerkleRoot.Should().Be(epoch2.MerkleRoot);
        epoch1.BlockCount.Should().Be(2);
        epoch1.TreeLevels.Should().HaveCount(2); // Level 0: 2 leaves, Level 1: 1 root
    }

    [Fact]
    public async Task GenerateEpochMerkleRoot_HandlesOddNumberOfLeavesCorrectly()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        await ledger.AppendAuditBlockAsync(_tenantId, "user1", "ACTION_A", "RES_1");
        await ledger.AppendAuditBlockAsync(_tenantId, "user2", "ACTION_B", "RES_2");
        await ledger.AppendAuditBlockAsync(_tenantId, "user3", "ACTION_C", "RES_3"); // 3 leaves = odd

        // Act
        var epoch = await ledger.GenerateEpochMerkleRootAsync(_tenantId);

        // Assert
        epoch.BlockCount.Should().Be(3);
        epoch.MerkleRoot.Should().NotBeNullOrWhiteSpace();
        epoch.TreeLevels.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task GenerateInclusionProof_GeneratesValidProof_AndVerifiesCorrectly()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        var b0 = await ledger.AppendAuditBlockAsync(_tenantId, "user0", "OP_0", "R_0");
        var b1 = await ledger.AppendAuditBlockAsync(_tenantId, "user1", "OP_1", "R_1");
        var b2 = await ledger.AppendAuditBlockAsync(_tenantId, "user2", "OP_2", "R_2");
        var b3 = await ledger.AppendAuditBlockAsync(_tenantId, "user3", "OP_3", "R_3");

        // Act
        var proof = await ledger.GenerateInclusionProofAsync(_tenantId, b2.EventId);

        // Assert
        proof.Should().NotBeNull();
        proof!.EventId.Should().Be(b2.EventId);
        proof.LeafIndex.Should().Be(2);
        proof.TotalLeaves.Should().Be(4);
        proof.ProofPath.Should().NotBeEmpty();

        bool isValid = ledger.VerifyInclusionProof(proof.LeafHash, proof.ProofPath, proof.MerkleRoot);
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyInclusionProof_WithTamperedLeafHash_ReturnsFalse()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        var b0 = await ledger.AppendAuditBlockAsync(_tenantId, "user0", "OP_0", "R_0");
        var b1 = await ledger.AppendAuditBlockAsync(_tenantId, "user1", "OP_1", "R_1");

        var proof = await ledger.GenerateInclusionProofAsync(_tenantId, b0.EventId);
        proof.Should().NotBeNull();

        // Act - Attempt to verify with a counterfeit leaf hash
        var fakeLeafHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("tampered_leaf"))).ToLowerInvariant();
        bool isValid = ledger.VerifyInclusionProof(fakeLeafHash, proof!.ProofPath, proof.MerkleRoot);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateInclusionProof_ForUnknownEventId_ReturnsNull()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        await ledger.AppendAuditBlockAsync(_tenantId, "user0", "OP_0", "R_0");

        // Act
        var proof = await ledger.GenerateInclusionProofAsync(_tenantId, Guid.NewGuid());

        // Assert
        proof.Should().BeNull();
    }

    [Fact]
    public async Task Ledger_SeparatesTenantsIndependently()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        await ledger.AppendAuditBlockAsync(_tenantId, "tenantA_user", "ACTION", "RES");
        await ledger.AppendAuditBlockAsync(_tenantIdB, "tenantB_user", "ACTION", "RES");

        // Act
        var blocksA = await ledger.GetBlocksAsync(_tenantId);
        var blocksB = await ledger.GetBlocksAsync(_tenantIdB);

        // Assert
        blocksA.Should().HaveCount(1);
        blocksB.Should().HaveCount(1);
        blocksA[0].Actor.Should().Be("tenantA_user");
        blocksB[0].Actor.Should().Be("tenantB_user");
    }

    #endregion

    #region 2. GDPR Right-to-be-Forgotten Cascade Tests

    [Fact]
    public async Task ExecuteRightToBeForgottenCascade_PseudonymizesContactFields()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var contact = Contact.Create(_tenantId, "Jean", "Dupont", "jean.dupont@enterprise.fr", "+33612345678");
        contact.Country = "France";
        contact.Tags = ["vip", "lead"];
        await db.Contacts.AddAsync(contact);
        await db.SaveChangesAsync();

        var ledger = new MerkleComplianceAuditLedgerService();
        var cascadeService = new GdprRightToBeForgottenCascadeService(db, ledger);

        // Act
        var result = await cascadeService.ExecuteRightToBeForgottenCascadeAsync(
            _tenantId,
            contact.Id,
            "jean.dupont@enterprise.fr",
            "DPO_Automation_Agent");

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.ContactsPseudonymized.Should().Be(1);

        var refreshed = await db.Contacts.FindAsync(contact.Id);
        refreshed.Should().NotBeNull();
        refreshed!.FirstName.Should().Be("GDPR_REDACTED");
        refreshed.LastName.Should().StartWith("GDPR_REDACTED_");
        refreshed.Email.Should().Contain("@pseudonymized.internal");
        refreshed.Phone.Should().BeNull();
        refreshed.Country.Should().Be("[REDACTED]");
        refreshed.Tags.Should().ContainSingle().Which.Should().Be("gdpr-erased");
    }

    [Fact]
    public async Task ExecuteRightToBeForgottenCascade_RedactsConversationsAndMessages()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var contact = Contact.Create(_tenantId, "Alice", "Smith", "alice.smith@client.com");
        await db.Contacts.AddAsync(contact);

        var inbox = new Inbox { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Support", ChannelType = ChannelType.Email };
        await db.Inboxes.AddAsync(inbox);

        var conv = Conversation.Create(_tenantId, inbox.Id, contact.Id);
        await db.Conversations.AddAsync(conv);

        var msg1 = Message.Create(_tenantId, conv.Id, MessageSenderType.Contact, "My personal address is 123 Main St, phone 555-0199");
        msg1.MediaUrl = "https://s3.amazonaws.com/uploads/passport.pdf";
        await db.Messages.AddAsync(msg1);

        var msg2 = Message.Create(_tenantId, conv.Id, MessageSenderType.Agent, "Thank you Alice, we have confirmed your order.");
        await db.Messages.AddAsync(msg2);

        await db.SaveChangesAsync();

        var cascadeService = new GdprRightToBeForgottenCascadeService(db);

        // Act
        var result = await cascadeService.ExecuteRightToBeForgottenCascadeAsync(
            _tenantId,
            contact.Id,
            "alice.smith@client.com",
            "PrivacyOfficer");

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.ConversationsPseudonymized.Should().Be(1);
        result.Certificate.MessagesPseudonymized.Should().Be(2);

        var refreshedMsg1 = await db.Messages.FindAsync(msg1.Id);
        refreshedMsg1!.Content.Should().Contain("[GDPR_REDACTED_");
        refreshedMsg1.Content.Should().NotContain("123 Main St");
        refreshedMsg1.MediaUrl.Should().BeNull();

        var refreshedConv = await db.Conversations.FindAsync(conv.Id);
        refreshedConv!.Status.Should().Be(ConversationStatus.Resolved);
    }

    [Fact]
    public async Task ExecuteRightToBeForgottenCascade_RedactsVoiceCallSessions()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var contact = Contact.Create(_tenantId, "Bob", "Builder", "bob@builder.com", "+12025550143");
        await db.Contacts.AddAsync(contact);

        var voiceCall = VoiceCallSession.Create(_tenantId, contact.Id, "CA12345678", CallDirection.Inbound, "+12025550143", "+18005550100");
        voiceCall.Transcript = "Customer stated his social security number is 987-65-4321.";
        voiceCall.RecordingUrl = "https://voice.storage.sgrowing.io/rec/CA12345678.mp3";
        await db.VoiceCallSessions.AddAsync(voiceCall);
        await db.SaveChangesAsync();

        var cascadeService = new GdprRightToBeForgottenCascadeService(db);

        // Act
        var result = await cascadeService.ExecuteRightToBeForgottenCascadeAsync(_tenantId, contact.Id, "bob@builder.com", "ComplianceBot");

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.VoiceTranscriptsPseudonymized.Should().Be(1);

        var refreshedVoice = await db.VoiceCallSessions.FindAsync(voiceCall.Id);
        refreshedVoice!.Transcript.Should().Contain("[GDPR_REDACTED_");
        refreshedVoice.Transcript.Should().NotContain("987-65-4321");
        refreshedVoice.RecordingUrl.Should().BeNull();
        refreshedVoice.FromNumber.Should().Be("[REDACTED]");
    }

    [Fact]
    public async Task ExecuteRightToBeForgottenCascade_RedactsDynamicFormsAndSurveys()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var contact = Contact.Create(_tenantId, "Charlie", "Brown", "charlie@brown.org");
        await db.Contacts.AddAsync(contact);

        var form = DynamicFormDefinition.Create(_tenantId, "Feedback", "feedback", "Customer survey", []);
        await db.DynamicFormDefinitions.AddAsync(form);

        var answers = new Dictionary<string, string> { ["name"] = "Charlie", ["feedback"] = "Great service!" };
        var submission = DynamicFormSubmission.Create(_tenantId, form.Id, answers, respondentContactId: contact.Id, respondentIp: "192.168.1.100");
        await db.DynamicFormSubmissions.AddAsync(submission);

        var surveyForm = SurveyForm.Create(_tenantId, "CSAT", SurveyType.Csat, "Rate us");
        await db.SurveyForms.AddAsync(surveyForm);

        var surveyResponse = SurveyResponse.Create(_tenantId, surveyForm.Id, contact.Id, 5, "Personal feedback from Charlie", FeedbackSentiment.Positive);
        await db.SurveyResponses.AddAsync(surveyResponse);

        await db.SaveChangesAsync();

        var cascadeService = new GdprRightToBeForgottenCascadeService(db);

        // Act
        var result = await cascadeService.ExecuteRightToBeForgottenCascadeAsync(_tenantId, contact.Id, "charlie@brown.org", "PrivacyOfficer");

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.FormSubmissionsPseudonymized.Should().Be(1);
        result.Certificate.SurveyResponsesPseudonymized.Should().Be(1);

        var refreshedSub = await db.DynamicFormSubmissions.FindAsync(submission.Id);
        refreshedSub!.RespondentContactId.Should().BeNull();
        refreshedSub.RespondentIp.Should().Be("0.0.0.0");
        refreshedSub.AnswersJson.Should().Contain("PSEUDONYMIZED");

        var refreshedSurvey = await db.SurveyResponses.FindAsync(surveyResponse.Id);
        refreshedSurvey!.FeedbackText.Should().Contain("[GDPR_REDACTED_");
    }

    [Fact]
    public async Task ExecuteRightToBeForgottenCascade_RedactsDigitalContractsAndSignatures()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var contact = Contact.Create(_tenantId, "David", "Miller", "david.miller@lawfirm.com");
        await db.Contacts.AddAsync(contact);

        var contract = DigitalContract.Create(_tenantId, "Consulting MSA", "MSA", "David Miller", "david.miller@lawfirm.com", 25000m);
        contract.SignedPdfUrl = "https://vault.storage.sgrowing.io/contracts/signed_msa.pdf";
        await db.DigitalContracts.AddAsync(contract);

        var envelope = DocumentEnvelope.Create(_tenantId, "NDA", "https://docs.sgrowing.io/nda.pdf", "raw_content");
        await db.DocumentEnvelopes.AddAsync(envelope);

        var signer = EnvelopeSigner.Create(_tenantId, envelope.Id, "David Miller", "david.miller@lawfirm.com");
        signer.SignatureData = "BASE64_SIGNATURE_VECTOR_DATA";
        signer.IpAddress = "82.165.197.1";
        await db.EnvelopeSigners.AddAsync(signer);

        await db.SaveChangesAsync();

        var cascadeService = new GdprRightToBeForgottenCascadeService(db);

        // Act
        var result = await cascadeService.ExecuteRightToBeForgottenCascadeAsync(_tenantId, contact.Id, "david.miller@lawfirm.com", "LegalCompliance");

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.ElectronicSignaturesPseudonymized.Should().Be(2);

        var refreshedContract = await db.DigitalContracts.FindAsync(contract.Id);
        refreshedContract!.SignerName.Should().StartWith("GDPR_REDACTED_");
        refreshedContract.SignerEmail.Should().Contain("@pseudonymized.internal");
        refreshedContract.SignedPdfUrl.Should().BeNull();

        var refreshedSigner = await db.EnvelopeSigners.FindAsync(signer.Id);
        refreshedSigner!.Name.Should().StartWith("GDPR_REDACTED_");
        refreshedSigner.SignatureData.Should().Be("[GDPR_REDACTED]");
        refreshedSigner.IpAddress.Should().Be("0.0.0.0");
    }

    [Fact]
    public async Task ExecuteRightToBeForgottenCascade_StrictlyPreservesFinancialLedgers_WhileStrippingPii()
    {
        // Arrange - statutory tax compliance under GDPR Art. 17(3)(b)
        using var db = CreateInMemoryDbContext();
        var contact = Contact.Create(_tenantId, "Eve", "Sterling", "eve.sterling@corporation.co.uk");
        await db.Contacts.AddAsync(contact);

        var invoice = new InvoiceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ContactId = contact.Id,
            InvoiceNumber = "INV-2026-UK-08492",
            ClientName = "Eve Sterling",
            ClientEmail = "eve.sterling@corporation.co.uk",
            Currency = "GBP",
            SubTotal = 1000.00m,
            TaxPercentage = 20.0m,
            TaxAmount = 200.00m,
            TotalAmount = 1200.00m,
            Status = InvoiceStatus.Paid,
            Notes = "Custom invoice issued for enterprise onboarding of Eve Sterling at London HQ"
        };
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();

        var cascadeService = new GdprRightToBeForgottenCascadeService(db);

        // Act
        var result = await cascadeService.ExecuteRightToBeForgottenCascadeAsync(_tenantId, contact.Id, "eve.sterling@corporation.co.uk", "AuditorAgent");

        // Assert
        result.Success.Should().BeTrue();
        result.Certificate.FinancialLedgersPreservedAndStripped.Should().Be(1);

        var refreshedInv = await db.Invoices.FindAsync(invoice.Id);
        refreshedInv.Should().NotBeNull();

        // 1. STATUTORY FINANCIAL LEDGER DATA MUST BE PRESERVED
        refreshedInv!.InvoiceNumber.Should().Be("INV-2026-UK-08492");
        refreshedInv.SubTotal.Should().Be(1000.00m);
        refreshedInv.TaxPercentage.Should().Be(20.0m);
        refreshedInv.TaxAmount.Should().Be(200.00m);
        refreshedInv.TotalAmount.Should().Be(1200.00m);
        refreshedInv.Currency.Should().Be("GBP");
        refreshedInv.Status.Should().Be(InvoiceStatus.Paid);

        // 2. CUSTOMER PII MUST BE STRIPPED / PSEUDONYMIZED
        refreshedInv.ClientName.Should().StartWith("GDPR_REDACTED_");
        refreshedInv.ClientEmail.Should().Contain("@statutory.internal");
        refreshedInv.Notes.Should().NotContain("Eve Sterling");
        refreshedInv.Notes.Should().Contain("statutory accounting requirements");
        refreshedInv.ContactId.Should().BeNull(); // Detached from erased contact
    }

    [Fact]
    public async Task ExecuteRightToBeForgottenCascade_GeneratesImmutableCertificateAndMerkleBlock()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var contact = Contact.Create(_tenantId, "Frank", "Ocean", "frank@ocean.io");
        await db.Contacts.AddAsync(contact);
        await db.SaveChangesAsync();

        var ledger = new MerkleComplianceAuditLedgerService();
        var cascadeService = new GdprRightToBeForgottenCascadeService(db, ledger);

        // Act
        var result = await cascadeService.ExecuteRightToBeForgottenCascadeAsync(_tenantId, contact.Id, "frank@ocean.io", "SecurityOfficer");

        // Assert
        result.Certificate.Should().NotBeNull();
        result.Certificate.CertificateId.Should().NotBeEmpty();
        result.Certificate.DataSubjectEmailHash.Should().NotBeNullOrWhiteSpace();
        result.Certificate.ProofOfErasureSha256.Should().NotBeNullOrWhiteSpace();
        result.Certificate.VerificationSignature.Should().NotBeNullOrWhiteSpace();
        result.Certificate.Status.Should().Be("CERTIFIED_AND_PSEUDONYMIZED");

        // Verify Merkle Audit Ledger recorded the block
        var blocks = await ledger.GetBlocksAsync(_tenantId);
        blocks.Should().ContainSingle();
        blocks[0].Action.Should().Be("GDPR_RIGHT_TO_BE_FORGOTTEN_CASCADE");
        blocks[0].Resource.Should().Be($"Contact/{contact.Id}");
    }

    #endregion

    #region 3. Field-Level Multi-Tenant Envelope Encryption Tests

    [Fact]
    public async Task EnvelopeEncryption_EncryptAndDecrypt_RoundTripsSuccessfully()
    {
        // Arrange
        var service = new EnvelopeEncryptionService();
        string plaintext = "Customer SSN: 123-45-6789; CreditCard: 4111-2222-3333-4444";
        string fieldPurpose = "CustomerFinancialPii";

        // Act
        var encrypted = await service.EncryptSensitiveFieldAsync(_tenantId, plaintext, fieldPurpose);
        var decrypted = await service.DecryptSensitiveFieldAsync(_tenantId, encrypted);

        // Assert
        encrypted.Should().NotBeNull();
        encrypted.CiphertextBase64.Should().NotBeNullOrWhiteSpace();
        encrypted.EncryptedDekBase64.Should().NotBeNullOrWhiteSpace();
        encrypted.CiphertextBase64.Should().NotBe(plaintext);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public async Task EnvelopeEncryption_DifferentTenants_ProduceDifferentCiphertextsAndDeks()
    {
        // Arrange
        var service = new EnvelopeEncryptionService();
        string plaintext = "Shared secret configuration string 2026";
        string fieldPurpose = "ApiSecret";

        // Act
        var encA = await service.EncryptSensitiveFieldAsync(_tenantId, plaintext, fieldPurpose);
        var encB = await service.EncryptSensitiveFieldAsync(_tenantIdB, plaintext, fieldPurpose);

        // Assert
        encA.CiphertextBase64.Should().NotBe(encB.CiphertextBase64);
        encA.EncryptedDekBase64.Should().NotBe(encB.EncryptedDekBase64);
    }

    [Fact]
    public async Task EnvelopeEncryption_CrossTenantDecryption_ThrowsCryptographicException()
    {
        // Arrange
        var service = new EnvelopeEncryptionService();
        string plaintext = "Secret payload belonging to Tenant A";
        var encA = await service.EncryptSensitiveFieldAsync(_tenantId, plaintext, "Confidential");

        // Act & Assert - Tenant B attempts to decrypt Tenant A's payload
        var act = async () => await service.DecryptSensitiveFieldAsync(_tenantIdB, encA);
        await act.Should().ThrowAsync<CryptographicException>()
            .WithMessage("*Cross-tenant*");
    }

    [Fact]
    public async Task EnvelopeEncryption_TamperedCiphertext_ThrowsCryptographicException()
    {
        // Arrange
        var service = new EnvelopeEncryptionService();
        var enc = await service.EncryptSensitiveFieldAsync(_tenantId, "Sensitive data", "PII");

        // Tamper 1 byte of the ciphertext
        var rawCiphertext = Convert.FromBase64String(enc.CiphertextBase64);
        rawCiphertext[0] ^= 0xFF;
        var tamperedPayload = enc with { CiphertextBase64 = Convert.ToBase64String(rawCiphertext) };

        // Act & Assert
        var act = async () => await service.DecryptSensitiveFieldAsync(_tenantId, tamperedPayload);
        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task EnvelopeEncryption_TamperedAuthenticationTag_ThrowsCryptographicException()
    {
        // Arrange
        var service = new EnvelopeEncryptionService();
        var enc = await service.EncryptSensitiveFieldAsync(_tenantId, "Sensitive data", "PII");

        // Tamper 1 byte of the GCM authentication tag
        var rawTag = Convert.FromBase64String(enc.TagBase64);
        rawTag[0] ^= 0xFF;
        var tamperedPayload = enc with { TagBase64 = Convert.ToBase64String(rawTag) };

        // Act & Assert
        var act = async () => await service.DecryptSensitiveFieldAsync(_tenantId, tamperedPayload);
        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task EnvelopeEncryption_TamperedWrappedDek_ThrowsCryptographicException()
    {
        // Arrange
        var service = new EnvelopeEncryptionService();
        var enc = await service.EncryptSensitiveFieldAsync(_tenantId, "Sensitive data", "PII");

        // Tamper wrapped DEK string
        var parts = enc.EncryptedDekBase64.Split('.');
        var rawWrappedDek = Convert.FromBase64String(parts[1]);
        rawWrappedDek[0] ^= 0xFF;
        var tamperedWrappedDek = $"{parts[0]}.{Convert.ToBase64String(rawWrappedDek)}.{parts[2]}";

        var tamperedPayload = enc with { EncryptedDekBase64 = tamperedWrappedDek };

        // Act & Assert
        var act = async () => await service.DecryptSensitiveFieldAsync(_tenantId, tamperedPayload);
        await act.Should().ThrowAsync<CryptographicException>();
    }

    #endregion

    #region 4. Verifiable Compliance Certificate Generator Tests

    [Fact]
    public async Task GenerateCertificateBundle_Soc2TypeII_GeneratesCompleteVerifiableBundle()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        await ledger.AppendAuditBlockAsync(_tenantId, "admin", "INIT", "Policy/SOC2");
        await ledger.AppendAuditBlockAsync(_tenantId, "auditor", "AUDIT_PASS", "Evidence/CC6.1");

        var certService = new ComplianceCertificateGeneratorService(ledger);

        // Act
        var bundle = await certService.GenerateCertificateBundleAsync(_tenantId, "SOC2_TYPE_II", "KPMG Sovereign Audit");

        // Assert
        bundle.Should().NotBeNull();
        bundle.CertificateId.Should().NotBeEmpty();
        bundle.ComplianceStandard.Should().Be("SOC2_TYPE_II");
        bundle.AuditorOrganization.Should().Be("KPMG Sovereign Audit");
        bundle.AuditLedgerBlocksVerified.Should().Be(2);
        bundle.LedgerIntegrityVerified.Should().BeTrue();
        bundle.MerkleLedgerRoot.Should().NotBeNullOrWhiteSpace();
        bundle.SecurityPolicyManifestHash.Should().NotBeNullOrWhiteSpace();
        bundle.EnforcedPolicies.Should().HaveCount(6);
        bundle.TenantIsolation.RlsEnforced.Should().BeTrue();
        bundle.CertificateSignatureSha256.Should().NotBeNullOrWhiteSpace();
        bundle.VerificationToken.Should().StartWith("SGCERT-SOC2-");
        bundle.Status.Should().Be("ACTIVE_CERTIFIED");
    }

    [Fact]
    public async Task GenerateCertificateBundle_Iso27001_GeneratesVerifiableBundle()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        await ledger.AppendAuditBlockAsync(_tenantId, "ciso", "POLICY_ENFORCE", "ISO27001/A.8.24");

        var certService = new ComplianceCertificateGeneratorService(ledger);

        // Act
        var bundle = await certService.GenerateCertificateBundleAsync(_tenantId, "ISO27001_2022");

        // Assert
        bundle.ComplianceStandard.Should().Be("ISO27001_2022");
        bundle.EnforcedPolicies.Should().HaveCount(5);
        bundle.EnforcedPolicies.Should().Contain(p => p.PolicyCode == "A.8.24");

        bool isValid = certService.VerifyCertificateBundle(bundle);
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyCertificateBundle_WhenBundleIsTampered_ReturnsFalse()
    {
        // Arrange
        var ledger = new MerkleComplianceAuditLedgerService();
        await ledger.AppendAuditBlockAsync(_tenantId, "admin", "INIT", "Policy/CC6.1");

        var certService = new ComplianceCertificateGeneratorService(ledger);
        var bundle = await certService.GenerateCertificateBundleAsync(_tenantId, "SOC2_TYPE_II");

        // Act 1: Verify untampered
        certService.VerifyCertificateBundle(bundle).Should().BeTrue();

        // Act 2: Tamper security policy manifest hash
        var tamperedPolicyBundle = bundle with { SecurityPolicyManifestHash = "0000000000000000000000000000000000000000000000000000000000000000" };
        certService.VerifyCertificateBundle(tamperedPolicyBundle).Should().BeFalse();

        // Act 3: Tamper signature
        var tamperedSigBundle = bundle with { CertificateSignatureSha256 = "deadbeefdeadbeefdeadbeefdeadbeef" };
        certService.VerifyCertificateBundle(tamperedSigBundle).Should().BeFalse();
    }

    #endregion
}
