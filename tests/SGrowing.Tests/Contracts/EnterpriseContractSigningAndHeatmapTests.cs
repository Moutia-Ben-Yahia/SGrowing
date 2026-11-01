using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Contracts;
using Xunit;

namespace SGrowing.Tests.Contracts;

public class EnterpriseContractSigningAndHeatmapTests
{
    private readonly MultiPartySigningWorkflowService _workflowService;
    private readonly CryptographicDocumentSealingService _sealingService;
    private readonly DocumentViewerTelemetryAndHeatmapService _telemetryService;
    private readonly Mock<IHubContext<OmniHub, IOmniClient>> _mockHubContext;
    private readonly Mock<IHubClients<IOmniClient>> _mockClients;
    private readonly Mock<IOmniClient> _mockClientProxy;

    private readonly Guid _testTenantId = Guid.NewGuid();

    public EnterpriseContractSigningAndHeatmapTests()
    {
        _workflowService = new MultiPartySigningWorkflowService();
        _sealingService = new CryptographicDocumentSealingService(_workflowService);

        _mockHubContext = new Mock<IHubContext<OmniHub, IOmniClient>>();
        _mockClients = new Mock<IHubClients<IOmniClient>>();
        _mockClientProxy = new Mock<IOmniClient>();

        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        _telemetryService = new DocumentViewerTelemetryAndHeatmapService(
            _mockHubContext.Object,
            _workflowService
        );
    }

    #region 1. Multi-Party Signing Workflow State Machine Tests

    [Fact]
    public async Task SequentialWorkflow_EnforcesStrictSigningOrder()
    {
        // Arrange
        var request = new CreateMultiPartyWorkflowRequest(
            _testTenantId,
            "Master Services Agreement 2026",
            "Sequential execution agreement between Client, Vendor, and Witness",
            "MSA",
            SignerWorkflowMode.Sequential,
            [
                new() { PageNumber = 1, Title = "General Terms", Content = "Terms and conditions" },
                new() { PageNumber = 2, Title = "Signatures", Content = "Execution signatures" }
            ],
            [
                new("Alice Client", "alice@client.com", "+14155551001", ContractSignerRole.ClientSigner, 0),
                new("Bob Vendor", "bob@vendor.com", "+14155551002", ContractSignerRole.VendorSigner, 1),
                new("Carol Witness", "carol@notary.com", "+14155551003", ContractSignerRole.Witness, 2)
            ]
        );

        // Act
        var envelope = await _workflowService.CreateWorkflowAsync(request);

        // Assert: Signer 0 is active, Signers 1 and 2 are in queue
        envelope.Should().NotBeNull();
        envelope.WorkflowMode.Should().Be(SignerWorkflowMode.Sequential);
        envelope.CurrentSignerIndex.Should().Be(0);
        envelope.Status.Should().Be(MultiPartyContractStatus.InProgress);
        envelope.Signers.Should().HaveCount(3);

        // Bob (Signer 1) attempts to sign out of order
        var bobSigner = envelope.Signers[1];
        var outOfTurnSubmit = await _workflowService.SubmitSignatureAsync(_testTenantId, envelope.Id, new SubmitMultiPartySignatureRequest(
            bobSigner.Token,
            "DrawnVector",
            "M10 20 Q 30 5 90 20"
        ));

        outOfTurnSubmit.Success.Should().BeFalse();
        outOfTurnSubmit.Message.Should().Contain("Out-of-turn signature rejected");
        bobSigner.LifecycleStatus.Should().NotBe(SignerLifecycleStatus.FullySigned);

        // Alice (Signer 0) signs legitimately
        var aliceSigner = envelope.Signers[0];
        var aliceSubmit = await _workflowService.SubmitSignatureAsync(_testTenantId, envelope.Id, new SubmitMultiPartySignatureRequest(
            aliceSigner.Token,
            "DrawnVector",
            "M5 25 Q 40 10 100 25"
        ));

        aliceSubmit.Success.Should().BeTrue();
        aliceSubmit.SignerStatus.Should().Be(SignerLifecycleStatus.FullySigned);
        aliceSubmit.NextSignerIndex.Should().Be(1);
        aliceSubmit.IsFullyExecuted.Should().BeFalse();

        // After Alice signs, Bob is now invited and can sign
        var reloaded = await _workflowService.GetWorkflowAsync(_testTenantId, envelope.Id);
        reloaded!.CurrentSignerIndex.Should().Be(1);
        reloaded.Signers[1].LifecycleStatus.Should().Be(SignerLifecycleStatus.Invited);

        var bobValidSubmit = await _workflowService.SubmitSignatureAsync(_testTenantId, envelope.Id, new SubmitMultiPartySignatureRequest(
            reloaded.Signers[1].Token,
            "Typed",
            "Robert Vance"
        ));

        bobValidSubmit.Success.Should().BeTrue();
        bobValidSubmit.SignerStatus.Should().Be(SignerLifecycleStatus.FullySigned);
        bobValidSubmit.NextSignerIndex.Should().Be(2);

        // Carol (Signer 2) signs to complete workflow
        reloaded = await _workflowService.GetWorkflowAsync(_testTenantId, envelope.Id);
        var carolSubmit = await _workflowService.SubmitSignatureAsync(_testTenantId, envelope.Id, new SubmitMultiPartySignatureRequest(
            reloaded!.Signers[2].Token,
            "DrawnVector",
            "M15 35 Q 50 15 120 35"
        ));

        carolSubmit.Success.Should().BeTrue();
        carolSubmit.IsFullyExecuted.Should().BeTrue();
        carolSubmit.Status.Should().Be(MultiPartyContractStatus.Completed);
        carolSubmit.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ParallelWorkflow_AllowsSignersToExecuteInAnyOrderConcurrently()
    {
        // Arrange
        var request = new CreateMultiPartyWorkflowRequest(
            _testTenantId,
            "Mutual Non-Disclosure Agreement",
            "Parallel signing NDA between two executives",
            "NDA",
            SignerWorkflowMode.Parallel,
            [
                new() { PageNumber = 1, Title = "Confidentiality Clauses", Content = "Trade secrets definition." }
            ],
            [
                new("David Founder", "david@startup.io", "+14155552001", ContractSignerRole.ClientSigner, 0),
                new("Elena Partner", "elena@venture.com", "+14155552002", ContractSignerRole.VendorSigner, 1)
            ]
        );

        var envelope = await _workflowService.CreateWorkflowAsync(request);

        // Assert both signers have active tokens generated at start
        envelope.WorkflowMode.Should().Be(SignerWorkflowMode.Parallel);
        envelope.Signers[0].Token.Should().NotBeNullOrWhiteSpace();
        envelope.Signers[1].Token.Should().NotBeNullOrWhiteSpace();

        // Elena (Signer 1) signs first (reverse order)
        var elenaSubmit = await _workflowService.SubmitSignatureAsync(_testTenantId, envelope.Id, new SubmitMultiPartySignatureRequest(
            envelope.Signers[1].Token,
            "DrawnVector",
            "M10 10 L 50 50"
        ));

        elenaSubmit.Success.Should().BeTrue();
        elenaSubmit.IsFullyExecuted.Should().BeFalse();
        elenaSubmit.Status.Should().Be(MultiPartyContractStatus.PartiallySigned);

        // David (Signer 0) signs second
        var davidSubmit = await _workflowService.SubmitSignatureAsync(_testTenantId, envelope.Id, new SubmitMultiPartySignatureRequest(
            envelope.Signers[0].Token,
            "Typed",
            "David Sterling"
        ));

        davidSubmit.Success.Should().BeTrue();
        davidSubmit.IsFullyExecuted.Should().BeTrue();
        davidSubmit.Status.Should().Be(MultiPartyContractStatus.Completed);
        davidSubmit.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SignerLifecycle_TransitionsFromInvitedToViewed()
    {
        // Arrange
        var request = new CreateMultiPartyWorkflowRequest(
            _testTenantId,
            "Software License",
            "Single Signer Test",
            "License",
            SignerWorkflowMode.Sequential,
            [new() { PageNumber = 1, Title = "EULA", Content = "License terms" }],
            [new("Grace Hopper", "grace@navy.mil", "+14155553001", ContractSignerRole.ClientSigner, 0)]
        );

        var envelope = await _workflowService.CreateWorkflowAsync(request);
        var signer = envelope.Signers[0];
        signer.LifecycleStatus.Should().Be(SignerLifecycleStatus.Invited);

        // Act: Signer opens viewer
        var viewedSigner = await _workflowService.RecordSignerViewedAsync(
            _testTenantId,
            envelope.Id,
            signer.Token,
            "192.168.1.100",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            "Washington, DC, US"
        );

        // Assert
        viewedSigner.Should().NotBeNull();
        viewedSigner!.LifecycleStatus.Should().Be(SignerLifecycleStatus.Viewed);
        viewedSigner.ViewedAtUtc.Should().NotBeNull();
        viewedSigner.IpAddress.Should().Be("192.168.1.100");
    }

    [Fact]
    public async Task SignerLifecycle_PartialSignature_TransitionsToPartiallySigned()
    {
        // Arrange
        var request = new CreateMultiPartyWorkflowRequest(
            _testTenantId,
            "Partnership Agreement",
            "Partial signing workflow",
            "Partnership",
            SignerWorkflowMode.Sequential,
            [new() { PageNumber = 1, Title = "Preamble", Content = "Recitals" }],
            [new("Hannah Abbot", "hannah@firm.com", "+14155554001", ContractSignerRole.ClientSigner, 0)]
        );

        var envelope = await _workflowService.CreateWorkflowAsync(request);
        var signer = envelope.Signers[0];

        // Act: Signer saves initials (partial signature)
        var partialResult = await _workflowService.SubmitSignatureAsync(_testTenantId, envelope.Id, new SubmitMultiPartySignatureRequest(
            signer.Token,
            "DrawnVector",
            "M1 1 Q 5 5 10 1",
            IsPartialSignature: true
        ));

        // Assert
        partialResult.Success.Should().BeTrue();
        partialResult.SignerStatus.Should().Be(SignerLifecycleStatus.PartiallySigned);
        partialResult.IsFullyExecuted.Should().BeFalse();
        partialResult.Status.Should().Be(MultiPartyContractStatus.PartiallySigned);
    }

    [Fact]
    public async Task DeclineSignature_TransitionsSignerAndEnvelopeToDeclined()
    {
        // Arrange
        var request = new CreateMultiPartyWorkflowRequest(
            _testTenantId,
            "Procurement Order",
            "Decline test agreement",
            "Order",
            SignerWorkflowMode.Sequential,
            [new() { PageNumber = 1, Title = "Terms", Content = "Payment within 30 days" }],
            [
                new("Ian Malcolm", "ian@jurassic.org", "+14155555001", ContractSignerRole.ClientSigner, 0),
                new("John Hammond", "john@ingen.com", "+14155555002", ContractSignerRole.VendorSigner, 1)
            ]
        );

        var envelope = await _workflowService.CreateWorkflowAsync(request);
        var signer = envelope.Signers[0];

        // Act: Signer declines with reason
        var declineResult = await _workflowService.DeclineSignatureAsync(_testTenantId, envelope.Id, new DeclineMultiPartySignatureRequest(
            signer.Token,
            "Payment terms are unacceptable. Need Net 90 instead of Net 30.",
            "10.0.0.50",
            "Chrome/130.0"
        ));

        // Assert
        declineResult.Success.Should().BeTrue();
        declineResult.Status.Should().Be(MultiPartyContractStatus.Declined);
        declineResult.DeclinedBySigner.Should().Be("Ian Malcolm");
        declineResult.Reason.Should().Contain("Net 90");

        var reloaded = await _workflowService.GetWorkflowAsync(_testTenantId, envelope.Id);
        reloaded!.Status.Should().Be(MultiPartyContractStatus.Declined);
        reloaded.Signers[0].LifecycleStatus.Should().Be(SignerLifecycleStatus.Declined);
        reloaded.Signers[0].DeclineReason.Should().Be("Payment terms are unacceptable. Need Net 90 instead of Net 30.");
        reloaded.AuditTrail.Should().Contain(a => a.Action == "SignatureDeclined");
    }

    #endregion

    #region 2. HMAC-SHA256 Token Generation & Expiration Tests

    [Fact]
    public void SigningLink_GeneratesValidHmacSha256Token()
    {
        // Arrange
        var contractId = Guid.NewGuid();
        var signerId = Guid.NewGuid();

        // Act
        var link = _workflowService.GenerateSigningLink(
            _testTenantId,
            contractId,
            signerId,
            0,
            "recipient@domain.com",
            TimeSpan.FromHours(72)
        );

        // Assert
        link.Should().NotBeNull();
        link.Token.Should().Contain(".");
        link.Url.Should().Contain(link.Token);
        link.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(71));

        var isValid = _workflowService.ValidateSigningToken(link.Token, out var payload, out var error);
        isValid.Should().BeTrue();
        error.Should().BeNull();
        payload.Should().NotBeNull();
        payload!.ContractId.Should().Be(contractId);
        payload.SignerId.Should().Be(signerId);
        payload.TenantId.Should().Be(_testTenantId);
    }

    [Fact]
    public void SigningLink_ExpiredToken_FailsValidationAndReturnsReason()
    {
        // Arrange: Generate link with negative expiration window (already expired)
        var contractId = Guid.NewGuid();
        var signerId = Guid.NewGuid();

        var link = _workflowService.GenerateSigningLink(
            _testTenantId,
            contractId,
            signerId,
            0,
            "late@domain.com",
            TimeSpan.FromSeconds(-10) // Expired 10 seconds ago
        );

        // Act
        var isValid = _workflowService.ValidateSigningToken(link.Token, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        payload.Should().BeNull();
        error.Should().Contain("expired");
    }

    [Fact]
    public void SigningLink_TamperedTokenPayload_RejectsCryptographically()
    {
        // Arrange
        var link = _workflowService.GenerateSigningLink(
            _testTenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "target@domain.com"
        );

        var parts = link.Token.Split('.');
        // Tamper with the payload part (swap a character)
        var tamperedPayload = parts[0][..^1] + (parts[0][^1] == 'A' ? 'B' : 'A');
        var tamperedToken = $"{tamperedPayload}.{parts[1]}";

        // Act
        var isValid = _workflowService.ValidateSigningToken(tamperedToken, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        payload.Should().BeNull();
        error.Should().Contain("HMAC signature");
    }

    [Fact]
    public void SigningLink_TamperedHmacSignature_RejectsCryptographically()
    {
        // Arrange
        var link = _workflowService.GenerateSigningLink(
            _testTenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "target@domain.com"
        );

        var parts = link.Token.Split('.');
        // Tamper with signature part
        var tamperedSignature = parts[1][..^1] + (parts[1][^1] == 'X' ? 'Y' : 'X');
        var tamperedToken = $"{parts[0]}.{tamperedSignature}";

        // Act
        var isValid = _workflowService.ValidateSigningToken(tamperedToken, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        payload.Should().BeNull();
        error.Should().Contain("HMAC signature");
    }

    [Fact]
    public async Task NotificationTriggers_Sequential_DispatchesWhatsAppEmailSmsAtEachAdvance()
    {
        // Arrange
        var request = new CreateMultiPartyWorkflowRequest(
            _testTenantId,
            "Enterprise Service Agreement",
            "Multi-channel trigger test",
            "ESA",
            SignerWorkflowMode.Sequential,
            [new() { PageNumber = 1, Title = "Scope", Content = "Cloud onboarding" }],
            [
                new("Jack Ryan", "jack@cia.gov", "+12025550143", ContractSignerRole.ClientSigner, 0),
                new("James Greer", "greer@cia.gov", "+12025550199", ContractSignerRole.VendorSigner, 1)
            ]
        );

        // Act: Workflow creation triggers Signer 0 notifications
        var envelope = await _workflowService.CreateWorkflowAsync(request);
        var signer0 = envelope.Signers[0];

        // Assert Signer 0 received WhatsApp, Email, SMS notifications
        signer0.NotificationHistory.Should().HaveCount(3);
        signer0.NotificationHistory.Should().Contain(n => n.Channel == NotificationChannel.Email && n.TriggerType == NotificationTriggerType.Invitation);
        signer0.NotificationHistory.Should().Contain(n => n.Channel == NotificationChannel.WhatsApp && n.TriggerType == NotificationTriggerType.Invitation);
        signer0.NotificationHistory.Should().Contain(n => n.Channel == NotificationChannel.Sms && n.TriggerType == NotificationTriggerType.Invitation);

        // Signer 1 hasn't received notifications yet
        envelope.Signers[1].NotificationHistory.Should().BeEmpty();

        // Signer 0 signs -> advances to Signer 1 -> triggers notifications for Signer 1
        await _workflowService.SubmitSignatureAsync(_testTenantId, envelope.Id, new SubmitMultiPartySignatureRequest(
            signer0.Token,
            "Typed",
            "Jack Ryan"
        ));

        var reloaded = await _workflowService.GetWorkflowAsync(_testTenantId, envelope.Id);
        var signer1 = reloaded!.Signers[1];
        signer1.NotificationHistory.Should().HaveCount(3);
        signer1.NotificationHistory.Should().Contain(n => n.Channel == NotificationChannel.WhatsApp);
    }

    #endregion

    #region 3. Cryptographic Sealing & Merkle Tree Integrity Tests

    [Fact]
    public async Task CryptographicSealing_GeneratesDeterministicMerkleRootAndEd25519Signature()
    {
        // Arrange
        var contractId = Guid.NewGuid();
        var proofs = new List<SignerExecutionProof>
        {
            new(
                Guid.NewGuid(),
                "Alice Smith",
                "alice@enterprise.com",
                "+14155557788",
                "198.51.100.4",
                "San Francisco, CA, US",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
                "M 10 50 C 20 20, 40 20, 50 50",
                "DrawnVector",
                DateTimeOffset.UtcNow.AddMinutes(-5)
            ),
            new(
                Guid.NewGuid(),
                "Bob Johnson",
                "bob@corp.com",
                "+14155559900",
                "203.0.113.19",
                "New York, NY, US",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                "M 0 0 L 100 100",
                "DrawnVector",
                DateTimeOffset.UtcNow.AddMinutes(-2)
            )
        };

        // Act
        var cert = await _sealingService.SealContractAsync(_testTenantId, contractId, proofs);

        // Assert
        cert.Should().NotBeNull();
        cert.CertificateId.Should().StartWith("CERT-SEAL-");
        cert.MerkleRootHash.Should().HaveLength(64); // SHA-256 hex string
        cert.DocumentSha256.Should().HaveLength(64);
        cert.CertificateSha256.Should().HaveLength(64);
        cert.DigitalSignatureBase64.Should().NotBeNullOrWhiteSpace();
        cert.PublicKeyBase64.Should().NotBeNullOrWhiteSpace();
        cert.NtpTimestampProof.Should().Contain("NTP-RFC5905-STRATUM1");
        cert.MerkleLeaves.Should().NotBeEmpty();
        cert.SignerManifests.Should().HaveCount(2);

        // Verify Leaf Hashes are present in MerkleLeaves
        cert.MerkleLeaves.Should().Contain(l => l.LeafType == "Page");
        cert.MerkleLeaves.Should().Contain(l => l.LeafType == "SignerProof");
    }

    [Fact]
    public async Task VerifyContractIntegrity_ValidSealedDocumentBytes_ReturnsTrue()
    {
        // Arrange
        var contractId = Guid.NewGuid();
        var proofs = new List<SignerExecutionProof>
        {
            new(
                Guid.NewGuid(),
                "Catherine Zeta",
                "cz@cinema.org",
                "+14155556633",
                "192.0.2.1",
                "London, UK",
                "Safari/605.1.15",
                "M 12 34 Q 56 78 90 12",
                "DrawnVector",
                DateTimeOffset.UtcNow
            )
        };

        var cert = await _sealingService.SealContractAsync(_testTenantId, contractId, proofs);

        // The document payload bytes that match the sealed contract
        var docPayloadString = $"DOC:{_testTenantId}:{contractId}:{cert.DocumentTitle}:{cert.MerkleRootHash}";
        var docBytes = Encoding.UTF8.GetBytes(docPayloadString);

        // Act
        var isIntegrityValid = await _sealingService.VerifyContractIntegrityAsync(
            _testTenantId,
            contractId,
            docBytes,
            cert.DocumentSha256
        );

        // Assert
        isIntegrityValid.Should().BeTrue();

        var detailedResult = await _sealingService.VerifyContractIntegrityDetailedAsync(
            _testTenantId,
            contractId,
            docBytes,
            cert.DocumentSha256
        );

        detailedResult.IsValid.Should().BeTrue();
        detailedResult.TamperDetected.Should().BeFalse();
        detailedResult.DigitalSignatureVerified.Should().BeTrue();
        detailedResult.MerkleRootVerified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyContractIntegrity_TamperedBytes_DetectsTamperAndReturnsFalse()
    {
        // Arrange
        var contractId = Guid.NewGuid();
        var proofs = new List<SignerExecutionProof>
        {
            new(
                Guid.NewGuid(),
                "Victor Frankenstein",
                "victor@geneva.ch",
                "+41225551234",
                "198.51.100.99",
                "Geneva, CH",
                "Firefox/132.0",
                "M 5 50 Q 50 5 95 50",
                "DrawnVector",
                DateTimeOffset.UtcNow
            )
        };

        var cert = await _sealingService.SealContractAsync(_testTenantId, contractId, proofs);

        // Original valid bytes
        var validPayload = $"DOC:{_testTenantId}:{contractId}:{cert.DocumentTitle}:{cert.MerkleRootHash}";
        var tamperedBytes = Encoding.UTF8.GetBytes(validPayload);
        // Tamper 1 byte
        tamperedBytes[0] ^= 0xFF;

        // Act
        var isIntegrityValid = await _sealingService.VerifyContractIntegrityAsync(
            _testTenantId,
            contractId,
            tamperedBytes,
            cert.DocumentSha256
        );

        // Assert
        isIntegrityValid.Should().BeFalse();

        var detailedResult = await _sealingService.VerifyContractIntegrityDetailedAsync(
            _testTenantId,
            contractId,
            tamperedBytes,
            cert.DocumentSha256
        );

        detailedResult.IsValid.Should().BeFalse();
        detailedResult.TamperDetected.Should().BeTrue();
        detailedResult.Message.Should().Contain("Verification failed");
    }

    [Fact]
    public void MerkleTree_ComputesRootCorrectlyForEvenAndOddLeaves()
    {
        // Even number of leaves (4)
        var leaves4 = new List<string>
        {
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Leaf1"))).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Leaf2"))).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Leaf3"))).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Leaf4"))).ToLowerInvariant()
        };

        var root4 = _sealingService.ComputeMerkleRoot(leaves4);
        root4.Should().HaveLength(64);

        // Odd number of leaves (3)
        var leaves3 = leaves4.Take(3).ToList();
        var root3 = _sealingService.ComputeMerkleRoot(leaves3);
        root3.Should().HaveLength(64);
        root3.Should().NotBe(root4);

        // Deterministic repetition
        var root4Repeat = _sealingService.ComputeMerkleRoot(leaves4);
        root4Repeat.Should().Be(root4);
    }

    #endregion

    #region 4. Document Viewer Telemetry & Heatmap Tests

    [Fact]
    public async Task Telemetry_RecordsPageByPageDwellAndCalculatesEngagementHeatmap()
    {
        // Arrange
        var contractId = Guid.NewGuid();

        // Simulate viewing session 1
        await _telemetryService.RecordPageViewAsync(new RecordPageViewRequest(
            _testTenantId,
            contractId,
            PageNumber: 1,
            DwellTimeSeconds: 15.0,
            ScrollDepthPercentage: 90.0,
            ZoomLevel: 1.0,
            ViewerIp: "10.0.0.1",
            ViewerCountry: "US",
            ViewerEmail: "lead@buyer.com",
            PageTitle: "Executive Summary"
        ));

        await _telemetryService.RecordPageViewAsync(new RecordPageViewRequest(
            _testTenantId,
            contractId,
            PageNumber: 2,
            DwellTimeSeconds: 120.0,
            ScrollDepthPercentage: 100.0,
            ZoomLevel: 1.25,
            ViewerIp: "10.0.0.1",
            ViewerCountry: "US",
            ViewerEmail: "lead@buyer.com",
            PageTitle: "Pricing & Tier Breakdown",
            IsPricingOrTerms: true
        ));

        await _telemetryService.RecordPageViewAsync(new RecordPageViewRequest(
            _testTenantId,
            contractId,
            PageNumber: 3,
            DwellTimeSeconds: 10.0,
            ScrollDepthPercentage: 50.0,
            ZoomLevel: 1.0,
            ViewerIp: "10.0.0.1",
            ViewerCountry: "US",
            ViewerEmail: "lead@buyer.com",
            PageTitle: "Appendix"
        ));

        // Act: Fetch aggregated heatmap
        var heatmap = await _telemetryService.GetContractHeatmapAsync(_testTenantId, contractId);

        // Assert
        heatmap.Should().NotBeNull();
        heatmap.ContractId.Should().Be(contractId);
        heatmap.TotalDwellTimeSeconds.Should().Be(145.0); // 15 + 120 + 10
        heatmap.TotalViews.Should().Be(3);
        heatmap.UniqueViewers.Should().Be(1);
        heatmap.PageHeatmaps.Should().HaveCount(3);

        // Page 2 should have the lion's share of engagement (>80%)
        var page2Heatmap = heatmap.PageHeatmaps.First(p => p.PageNumber == 2);
        page2Heatmap.TotalDwellTimeSeconds.Should().Be(120.0);
        page2Heatmap.IsPricingOrTermsPage.Should().BeTrue();
        page2Heatmap.AttentionSharePercentage.Should().BeGreaterThan(80.0);

        var page1Heatmap = heatmap.PageHeatmaps.First(p => p.PageNumber == 1);
        page1Heatmap.TotalDwellTimeSeconds.Should().Be(15.0);
        page1Heatmap.AttentionSharePercentage.Should().BeLessThan(15.0);
    }

    [Fact]
    public async Task Telemetry_HighIntentAlert_FiresWhenProspectSpendsOver60SecondsOnPricingPage()
    {
        // Arrange
        var contractId = Guid.NewGuid();

        // Act: Record 75 seconds dwell time on pricing page
        await _telemetryService.RecordPageViewAsync(new RecordPageViewRequest(
            _testTenantId,
            contractId,
            PageNumber: 2,
            DwellTimeSeconds: 75.0,
            ScrollDepthPercentage: 95.0,
            ViewerIp: "198.51.100.12",
            ViewerCountry: "AE",
            ViewerEmail: "cfo@enterprise.ae",
            PageTitle: "Commercial Pricing & License Tiers",
            IsPricingOrTerms: true
        ));

        // Assert: High-intent alert generated
        var alerts = await _telemetryService.GetHighIntentAlertsAsync(_testTenantId, contractId);
        alerts.Should().HaveCount(1);

        var alert = alerts[0];
        alert.DwellTimeSeconds.Should().Be(75.0);
        alert.PageNumber.Should().Be(2);
        alert.ViewerEmail.Should().Be("cfo@enterprise.ae");
        alert.TriggerReason.Should().Contain("High Purchase Intent");

        // Verify SignalR broadcast was dispatched to the tenant group
        _mockClients.Verify(c => c.Group(OmniHub.GetTenantGroup(_testTenantId)), Times.Once);
        _mockClientProxy.Verify(p => p.OnHighIntentProposalViewed(It.Is<HighIntentProposalViewedEventDto>(
            dto => dto.DwellSeconds == 75.0 && dto.PageNumber == 2 && dto.ViewerEmail == "cfo@enterprise.ae"
        )), Times.Once);
    }

    [Fact]
    public async Task Telemetry_HighIntentAlert_DoesNotFireOnNonPricingPageEvenIfDwellIsLong()
    {
        // Arrange
        var contractId = Guid.NewGuid();

        // Act: 120 seconds dwell on Page 1 (Intro/Overview, not pricing)
        await _telemetryService.RecordPageViewAsync(new RecordPageViewRequest(
            _testTenantId,
            contractId,
            PageNumber: 1,
            DwellTimeSeconds: 120.0,
            PageTitle: "Company Overview & Architecture",
            IsPricingOrTerms: false
        ));

        // Assert: No alert should be triggered
        var alerts = await _telemetryService.GetHighIntentAlertsAsync(_testTenantId, contractId);
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task Telemetry_HighIntentAlert_DoesNotFireIfPricingPageDwellIsUnder60Seconds()
    {
        // Arrange
        var contractId = Guid.NewGuid();

        // Act: 45 seconds dwell on Pricing page
        await _telemetryService.RecordPageViewAsync(new RecordPageViewRequest(
            _testTenantId,
            contractId,
            PageNumber: 2,
            DwellTimeSeconds: 45.0,
            PageTitle: "Pricing & Fees",
            IsPricingOrTerms: true
        ));

        // Assert: No alert should be triggered (< 60s threshold)
        var alerts = await _telemetryService.GetHighIntentAlertsAsync(_testTenantId, contractId);
        alerts.Should().BeEmpty();
    }

    [Fact]
    public void IsPricingOrTermsPage_DetectsKeyTerminology()
    {
        _telemetryService.IsPricingOrTermsPage(2, "Commercial Investment & Pricing").Should().BeTrue();
        _telemetryService.IsPricingOrTermsPage(3, "Legal Terms and Governing Law").Should().BeTrue();
        _telemetryService.IsPricingOrTermsPage(1, "Fee Schedule and Cost Breakdown").Should().BeTrue();
        _telemetryService.IsPricingOrTermsPage(1, "Executive Summary").Should().BeFalse();
        _telemetryService.IsPricingOrTermsPage(4, "Technical Architecture").Should().BeFalse();
    }

    #endregion

    #region 5. End-to-End Integrated Lifecycle Test

    [Fact]
    public async Task EndToEnd_CompleteContractLifecycle_Workflow_Sealing_Telemetry()
    {
        // 1. Create multi-party contract with 2 signers
        var workflow = await _workflowService.CreateWorkflowAsync(new CreateMultiPartyWorkflowRequest(
            _testTenantId,
            "Strategic Enterprise Alliance Agreement",
            "Full lifecycle demonstration",
            "Alliance",
            SignerWorkflowMode.Sequential,
            [
                new() { PageNumber = 1, Title = "Alliance Objectives", Content = "Joint GTM strategy" },
                new() { PageNumber = 2, Title = "Commercial Terms & Pricing", Content = "Revenue share 70/30" }
            ],
            [
                new("Elena Rostova", "elena@alliance.eu", "+442079460991", ContractSignerRole.ClientSigner, 0),
                new("Marcus Brody", "marcus@alliance.eu", "+442079460992", ContractSignerRole.VendorSigner, 1)
            ]
        ));

        workflow.Status.Should().Be(MultiPartyContractStatus.InProgress);

        // 2. Prospects view and examine pricing page in telemetry viewer
        var viewRecord = await _telemetryService.RecordPageViewAsync(new RecordPageViewRequest(
            _testTenantId,
            workflow.Id,
            PageNumber: 2,
            DwellTimeSeconds: 95.0,
            ViewerEmail: "elena@alliance.eu",
            PageTitle: "Commercial Terms & Pricing",
            IsPricingOrTerms: true
        ));

        viewRecord.Should().NotBeNull();
        var heatmap = await _telemetryService.GetContractHeatmapAsync(_testTenantId, workflow.Id);
        heatmap.TotalViews.Should().BeGreaterThan(0);
        heatmap.HighIntentAlertsCount.Should().Be(1);

        // 3. Elena (Signer 0) executes digital signature
        var elenaSignResult = await _workflowService.SubmitSignatureAsync(_testTenantId, workflow.Id, new SubmitMultiPartySignatureRequest(
            workflow.Signers[0].Token,
            "DrawnVector",
            "M10 80 Q 95 10 180 80",
            IpAddress: "82.165.197.1",
            UserAgent: "Mozilla/5.0 Chrome/130",
            Geolocation: "Berlin, DE"
        ));

        elenaSignResult.Success.Should().BeTrue();
        elenaSignResult.NextSignerIndex.Should().Be(1);

        // 4. Marcus (Signer 1) executes digital signature
        var updatedWorkflow = await _workflowService.GetWorkflowAsync(_testTenantId, workflow.Id);
        var marcusSignResult = await _workflowService.SubmitSignatureAsync(_testTenantId, workflow.Id, new SubmitMultiPartySignatureRequest(
            updatedWorkflow!.Signers[1].Token,
            "DrawnVector",
            "M20 70 Q 100 20 190 70",
            IpAddress: "82.165.197.2",
            UserAgent: "Mozilla/5.0 Safari/18",
            Geolocation: "London, UK"
        ));

        marcusSignResult.Success.Should().BeTrue();
        marcusSignResult.IsFullyExecuted.Should().BeTrue();
        marcusSignResult.Status.Should().Be(MultiPartyContractStatus.Completed);

        // 5. Seal completed contract with Merkle root and asymmetric signature
        var proofs = new List<SignerExecutionProof>
        {
            new(
                workflow.Signers[0].Id,
                "Elena Rostova",
                "elena@alliance.eu",
                "+442079460991",
                "82.165.197.1",
                "Berlin, DE",
                "Mozilla/5.0 Chrome/130",
                "M10 80 Q 95 10 180 80",
                "DrawnVector",
                DateTimeOffset.UtcNow.AddMinutes(-5)
            ),
            new(
                workflow.Signers[1].Id,
                "Marcus Brody",
                "marcus@alliance.eu",
                "+442079460992",
                "82.165.197.2",
                "London, UK",
                "Mozilla/5.0 Safari/18",
                "M20 70 Q 100 20 190 70",
                "DrawnVector",
                DateTimeOffset.UtcNow
            )
        };

        var cert = await _sealingService.SealContractAsync(_testTenantId, workflow.Id, proofs);
        cert.Should().NotBeNull();
        cert.MerkleRootHash.Should().NotBeNullOrWhiteSpace();

        // 6. Verify tamper-evident integrity
        var docPayload = $"DOC:{_testTenantId}:{workflow.Id}:{cert.DocumentTitle}:{cert.MerkleRootHash}";
        var docBytes = Encoding.UTF8.GetBytes(docPayload);

        var verifyResult = await _sealingService.VerifyContractIntegrityDetailedAsync(
            _testTenantId,
            workflow.Id,
            docBytes,
            cert.DocumentSha256
        );

        verifyResult.IsValid.Should().BeTrue();
        verifyResult.TamperDetected.Should().BeFalse();
        verifyResult.DigitalSignatureVerified.Should().BeTrue();
        verifyResult.MerkleRootVerified.Should().BeTrue();
    }

    #endregion
}
