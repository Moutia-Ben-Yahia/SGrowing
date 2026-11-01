using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Commerce.QualityGate;
using Xunit;

namespace SGrowing.Tests.Commerce;

/// <summary>
/// Comprehensive unit test suite for Phase 43:
/// Autonomous Multimodal Vision-Language Inspection &amp; Automated Warehouse Quality Gate.
/// </summary>
public sealed class WarehouseQualityGateAndVisionTests
{
    private readonly VisualPackagingAnomalyDetector _detector = new();
    private readonly CounterfeitHologramVerifierService _hologramVerifier = new();
    private readonly AutonomousGrnDiscrepancyRouter _grnRouter = new();

    private static byte[] CreateSynthesizedFrame(string directive)
    {
        var textBytes = Encoding.UTF8.GetBytes(directive);
        var buffer = new byte[Math.Max(128, textBytes.Length + 64)];
        Array.Copy(textBytes, buffer, textBytes.Length);
        for (int i = textBytes.Length; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(100 + (i % 30));
        }
        return buffer;
    }

    private static byte[] CreateCleanFrame()
    {
        return CreateSynthesizedFrame("[CLEAN]");
    }

    #region 1. Packaging Anomaly Detection Across Defect Types

    [Fact]
    public void AnalyzePackagingImage_CrushedBoxCorner_DetectsAnomalyAndCalculatesConfidence()
    {
        var frame = CreateSynthesizedFrame("[DEFECT:CrushedBoxCorner:0.92:Moderate]");
        var result = _detector.AnalyzePackagingImage("WB-CRUSH-001", frame);

        result.TotalDefectsFound.Should().Be(1);
        result.Findings.Should().ContainSingle();
        var finding = result.Findings[0];
        finding.DefectType.Should().Be(PackagingDefectType.CrushedBoxCorner);
        finding.Severity.Should().Be(DefectSeverity.Moderate);
        finding.ConfidenceScore.Should().Be(0.92);
        finding.BoundingBox.Should().NotBeNull();
        finding.BoundingBox.Width.Should().BeGreaterThan(0);
        finding.BoundingBox.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnalyzePackagingImage_TornCarton_DetectsAnomalyAndCalculatesConfidence()
    {
        var frame = CreateSynthesizedFrame("[DEFECT:TornCarton:0.85:Moderate]");
        var result = _detector.AnalyzePackagingImage("WB-TORN-002", frame);

        result.TotalDefectsFound.Should().Be(1);
        var finding = result.Findings[0];
        finding.DefectType.Should().Be(PackagingDefectType.TornCarton);
        finding.Severity.Should().Be(DefectSeverity.Moderate);
        finding.ConfidenceScore.Should().Be(0.85);
        finding.DefectDescription.Should().Contain("tear");
    }

    [Fact]
    public void AnalyzePackagingImage_PunctureHole_DetectsSevereAnomalyAndCalculatesConfidence()
    {
        var frame = CreateSynthesizedFrame("[DEFECT:PunctureHole:0.95:SevereCritical]");
        var result = _detector.AnalyzePackagingImage("WB-PUNCTURE-003", frame);

        result.TotalDefectsFound.Should().Be(1);
        var finding = result.Findings[0];
        finding.DefectType.Should().Be(PackagingDefectType.PunctureHole);
        finding.Severity.Should().Be(DefectSeverity.SevereCritical);
        finding.ConfidenceScore.Should().Be(0.95);
    }

    [Fact]
    public void AnalyzePackagingImage_BrokenSecurityTape_DetectsAnomalyAndCalculatesConfidence()
    {
        var frame = CreateSynthesizedFrame("[DEFECT:BrokenSecurityTape:0.88:Moderate]");
        var result = _detector.AnalyzePackagingImage("WB-TAPE-004", frame);

        result.TotalDefectsFound.Should().Be(1);
        var finding = result.Findings[0];
        finding.DefectType.Should().Be(PackagingDefectType.BrokenSecurityTape);
        finding.ConfidenceScore.Should().Be(0.88);
    }

    [Fact]
    public void AnalyzePackagingImage_LiquidSpillStain_DetectsAnomalyAndCalculatesConfidence()
    {
        var frame = CreateSynthesizedFrame("[DEFECT:LiquidSpillStain:0.80:Moderate]");
        var result = _detector.AnalyzePackagingImage("WB-SPILL-005", frame);

        result.TotalDefectsFound.Should().Be(1);
        var finding = result.Findings[0];
        finding.DefectType.Should().Be(PackagingDefectType.LiquidSpillStain);
        finding.ConfidenceScore.Should().Be(0.80);
    }

    [Fact]
    public void AnalyzePackagingImage_IllegibleBarcode_DetectsMinorAnomalyAndCalculatesConfidence()
    {
        var frame = CreateSynthesizedFrame("[DEFECT:IllegibleBarcode:0.90:Minor]");
        var result = _detector.AnalyzePackagingImage("WB-BARCODE-006", frame);

        result.TotalDefectsFound.Should().Be(1);
        var finding = result.Findings[0];
        finding.DefectType.Should().Be(PackagingDefectType.IllegibleBarcode);
        finding.Severity.Should().Be(DefectSeverity.Minor);
        finding.ConfidenceScore.Should().Be(0.90);
    }

    [Fact]
    public void AnalyzePackagingImage_MultipleSimultaneousDefects_AggregatesAllFindings()
    {
        var frame = CreateSynthesizedFrame("[DEFECT:CrushedBoxCorner:0.90:Moderate][DEFECT:BrokenSecurityTape:0.80:Moderate][DEFECT:LiquidSpillStain:0.75:Moderate]");
        var result = _detector.AnalyzePackagingImage("WB-MULTI-007", frame);

        result.TotalDefectsFound.Should().Be(3);
        result.Findings.Select(f => f.DefectType).Should().Contain([
            PackagingDefectType.CrushedBoxCorner,
            PackagingDefectType.BrokenSecurityTape,
            PackagingDefectType.LiquidSpillStain
        ]);
    }

    [Fact]
    public void AnalyzePackagingImage_CleanPackage_ReturnsZeroDefectsAndFullIntegrityScore()
    {
        var frame = CreateCleanFrame();
        var result = _detector.AnalyzePackagingImage("WB-CLEAN-008", frame);

        result.TotalDefectsFound.Should().Be(0);
        result.Findings.Should().BeEmpty();
        result.IntegrityScore.Should().Be(100.0);
        result.Disposition.Should().Be(InspectionDisposition.AcceptedDirectToStock);
    }

    #endregion

    #region 2. Mathematical Integrity Score Computation & Penalty Weights

    [Fact]
    public void IntegrityScore_MinorDefect_Applies5PointWeightMultiplier()
    {
        // Minor weight = 5.0, Confidence = 0.80 => Penalty = 4.0 => Score = 96.0
        var frame = CreateSynthesizedFrame("[DEFECT:IllegibleBarcode:0.80:Minor]");
        var result = _detector.AnalyzePackagingImage("WB-MATH-001", frame);

        result.IntegrityScore.Should().Be(96.0);
        result.Disposition.Should().Be(InspectionDisposition.AcceptedDirectToStock);
    }

    [Fact]
    public void IntegrityScore_ModerateDefect_Applies15PointWeightMultiplier()
    {
        // Moderate weight = 15.0, Confidence = 0.80 => Penalty = 12.0 => Score = 88.0
        var frame = CreateSynthesizedFrame("[DEFECT:CrushedBoxCorner:0.80:Moderate]");
        var result = _detector.AnalyzePackagingImage("WB-MATH-002", frame);

        result.IntegrityScore.Should().Be(88.0);
        result.Disposition.Should().Be(InspectionDisposition.AcceptedDirectToStock);
    }

    [Fact]
    public void IntegrityScore_SevereCriticalDefect_Applies35PointWeightMultiplier()
    {
        // SevereCritical weight = 35.0, Confidence = 0.90 => Penalty = 31.5 => Score = 68.5
        var frame = CreateSynthesizedFrame("[DEFECT:PunctureHole:0.90:SevereCritical]");
        var result = _detector.AnalyzePackagingImage("WB-MATH-003", frame);

        result.IntegrityScore.Should().Be(68.5);
        result.Disposition.Should().Be(InspectionDisposition.QuarantinePendingReview);
    }

    [Fact]
    public void IntegrityScore_CombinedDefects_CalculatesAccurateCumulativeDeduction()
    {
        // Minor (5 * 1.0 = 5.0) + Moderate (15 * 0.8 = 12.0) + Severe (35 * 0.8 = 28.0) => Total penalty = 45.0 => Score = 55.0
        var frame = CreateSynthesizedFrame("[DEFECT:IllegibleBarcode:1.0:Minor][DEFECT:CrushedBoxCorner:0.8:Moderate][DEFECT:PunctureHole:0.8:SevereCritical]");
        var result = _detector.AnalyzePackagingImage("WB-MATH-004", frame);

        result.IntegrityScore.Should().Be(55.0);
        result.Disposition.Should().Be(InspectionDisposition.RejectedReturnToVendor);
    }

    [Fact]
    public void IntegrityScore_ExtremeDefectPenalties_ClampsAtZeroWithoutUnderflow()
    {
        // 4 severe defects => penalty = 4 * 35 = 140 => raw = -40 => clamped to 0.0
        var frame = CreateSynthesizedFrame("[DEFECT:PunctureHole:1.0:SevereCritical][DEFECT:PunctureHole:1.0:SevereCritical][DEFECT:PunctureHole:1.0:SevereCritical][DEFECT:PunctureHole:1.0:SevereCritical]");
        var result = _detector.AnalyzePackagingImage("WB-MATH-005", frame);

        result.IntegrityScore.Should().Be(0.0);
        result.Disposition.Should().Be(InspectionDisposition.RejectedReturnToVendor);
    }

    [Fact]
    public void IntegrityScore_PerfectInspection_YieldsExactly100Score()
    {
        var frame = CreateCleanFrame();
        var result = _detector.AnalyzePackagingImage("WB-MATH-006", frame);

        result.IntegrityScore.Should().Be(100.0);
    }

    [Fact]
    public void IntegrityScore_RoundingPrecision_RoundsToTwoDecimalPlaces()
    {
        // Minor: 5 * 0.333 = 1.665 => Score = 100 - 1.665 = 98.335 => rounded to 98.34 or 98.33
        var frame = CreateSynthesizedFrame("[DEFECT:IllegibleBarcode:0.333:Minor]");
        var result = _detector.AnalyzePackagingImage("WB-MATH-007", frame);

        result.IntegrityScore.Should().BeInRange(98.33, 98.34);
    }

    #endregion

    #region 3. Disposition Thresholds & State Transition Mapping

    [Fact]
    public void Disposition_ScoreAtOrAbove85_MapsToAcceptedDirectToStock()
    {
        // Penalty = 5.0 * 1.0 = 5.0 => Score = 95.0 >= 85
        var frame = CreateSynthesizedFrame("[DEFECT:IllegibleBarcode:1.0:Minor]");
        var result = _detector.AnalyzePackagingImage("WB-DISP-001", frame);

        result.IntegrityScore.Should().Be(95.0);
        result.Disposition.Should().Be(InspectionDisposition.AcceptedDirectToStock);
    }

    [Fact]
    public void Disposition_ScoreExactly85_MapsToAcceptedDirectToStock_BoundaryTest()
    {
        // Penalty = 15.0 * 1.0 = 15.0 => Score = 85.0
        var frame = CreateSynthesizedFrame("[DEFECT:CrushedBoxCorner:1.0:Moderate]");
        var result = _detector.AnalyzePackagingImage("WB-DISP-002", frame);

        result.IntegrityScore.Should().Be(85.0);
        result.Disposition.Should().Be(InspectionDisposition.AcceptedDirectToStock);
    }

    [Fact]
    public void Disposition_ScoreBetween60And84Point99_MapsToQuarantinePendingReview()
    {
        // Penalty = 35.0 * 0.8 = 28.0 => Score = 72.0 (in [60, 85))
        var frame = CreateSynthesizedFrame("[DEFECT:PunctureHole:0.8:SevereCritical]");
        var result = _detector.AnalyzePackagingImage("WB-DISP-003", frame);

        result.IntegrityScore.Should().Be(72.0);
        result.Disposition.Should().Be(InspectionDisposition.QuarantinePendingReview);
    }

    [Fact]
    public void Disposition_ScoreExactly60_MapsToQuarantinePendingReview_BoundaryTest()
    {
        // Penalty = 40.0: Moderate (15 * 1.0) + Severe (35 * 0.7142857) or Two Moderates (30) + two minors (10) = 40
        var frame = CreateSynthesizedFrame("[DEFECT:CrushedBoxCorner:1.0:Moderate][DEFECT:BrokenSecurityTape:1.0:Moderate][DEFECT:IllegibleBarcode:1.0:Minor][DEFECT:IllegibleBarcode:1.0:Minor]");
        var result = _detector.AnalyzePackagingImage("WB-DISP-004", frame);

        result.IntegrityScore.Should().Be(60.0);
        result.Disposition.Should().Be(InspectionDisposition.QuarantinePendingReview);
    }

    [Fact]
    public void Disposition_ScoreBelow60_MapsToRejectedReturnToVendor()
    {
        // Penalty = 35.0 * 1.0 + 15.0 * 1.0 = 50.0 => Score = 50.0 < 60
        var frame = CreateSynthesizedFrame("[DEFECT:PunctureHole:1.0:SevereCritical][DEFECT:CrushedBoxCorner:1.0:Moderate]");
        var result = _detector.AnalyzePackagingImage("WB-DISP-005", frame);

        result.IntegrityScore.Should().Be(50.0);
        result.Disposition.Should().Be(InspectionDisposition.RejectedReturnToVendor);
    }

    #endregion

    #region 4. Image Frame Input Validation & Inspection History

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void AnalyzePackagingImage_NullOrWhitespaceWaybill_ThrowsArgumentException(string? waybill)
    {
        var frame = CreateCleanFrame();
        var act = () => _detector.AnalyzePackagingImage(waybill!, frame);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnalyzePackagingImage_NullOrEmptyBytes_ThrowsArgumentException()
    {
        var actNull = () => _detector.AnalyzePackagingImage("WB-VAL-001", null!);
        actNull.Should().Throw<ArgumentNullException>();

        var actEmpty = () => _detector.AnalyzePackagingImage("WB-VAL-002", []);
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetInspectionHistory_ExistingWaybill_ReturnsAllHistoricalInspectionsInOrder()
    {
        string waybill = "WB-HIST-100";
        var frame1 = CreateSynthesizedFrame("[DEFECT:IllegibleBarcode:0.5:Minor]");
        var frame2 = CreateSynthesizedFrame("[DEFECT:CrushedBoxCorner:0.9:Moderate]");

        var res1 = _detector.AnalyzePackagingImage(waybill, frame1);
        var res2 = _detector.AnalyzePackagingImage(waybill, frame2);

        var history = _detector.GetInspectionHistory(waybill);
        history.Should().HaveCount(2);
        history[0].InspectionId.Should().Be(res1.InspectionId);
        history[1].InspectionId.Should().Be(res2.InspectionId);
    }

    [Fact]
    public void GetInspectionHistory_NonExistentWaybill_ReturnsEmptyCollection()
    {
        var history = _detector.GetInspectionHistory("NON-EXISTENT-WAYBILL");
        history.Should().BeEmpty();
    }

    [Fact]
    public void GetInspectionById_ExistingId_ReturnsMatchingRecord()
    {
        var frame = CreateCleanFrame();
        var res = _detector.AnalyzePackagingImage("WB-LOOKUP-01", frame);

        var retrieved = _detector.GetInspectionById(res.InspectionId);
        retrieved.Should().NotBeNull();
        retrieved!.InspectionId.Should().Be(res.InspectionId);
        retrieved.ConsignmentWaybill.Should().Be("WB-LOOKUP-01");
    }

    #endregion

    #region 5. Optical Hologram Verification & Microtext Matching

    [Fact]
    public void VerifyHologram_AuthenticOpticsAndMicrotextMatch_ReturnsAuthenticVerified()
    {
        string serial = "SER-AUTH-777";
        string microtext = "SGROWING-SECURE-GENUINE-2026";
        var scan = CreateSynthesizedFrame($"[DIFFRACTION:0.92][MICROTEXT:PASS]{microtext}");

        var finding = _hologramVerifier.VerifyHologram(serial, scan, microtext);

        finding.Status.Should().Be(HologramVerificationStatus.AuthenticVerified);
        finding.DiffractionScore.Should().Be(0.92);
        finding.MicrotextHashMatch.Should().BeTrue();
        finding.SecurityFeaturesDetected.Should().Contain("DiffractionRainbowOptics");
        finding.SecurityFeaturesDetected.Should().Contain("NanoMicrotextResolution");
        finding.SecurityFeaturesDetected.Should().Contain("GuillocheDynamicInterferenceMatrix");
    }

    [Fact]
    public void VerifyHologram_MicrotextMismatch_ReturnsCounterfeitSuspect()
    {
        string serial = "SER-MISMATCH-123";
        string expectedMicrotext = "SGROWING-AUTHENTIC-KEY";
        var scan = CreateSynthesizedFrame("[DIFFRACTION:0.85][MICROTEXT:FAIL]");

        var finding = _hologramVerifier.VerifyHologram(serial, scan, expectedMicrotext);

        finding.Status.Should().Be(HologramVerificationStatus.CounterfeitSuspect);
        finding.MicrotextHashMatch.Should().BeFalse();
    }

    [Fact]
    public void VerifyHologram_LowDiffractionFlatPhotocopy_ReturnsCounterfeitSuspect()
    {
        string serial = "SER-PHOTOCOPY-456";
        string microtext = "PLAIN-TEXT";
        var scan = CreateSynthesizedFrame("[DIFFRACTION:0.22][MICROTEXT:PASS]");

        var finding = _hologramVerifier.VerifyHologram(serial, scan, microtext);

        finding.Status.Should().Be(HologramVerificationStatus.CounterfeitSuspect);
        finding.DiffractionScore.Should().Be(0.22);
    }

    [Fact]
    public void VerifyHologram_TamperedOrDefacedVoidPattern_ReturnsTamperedDefaced()
    {
        string serial = "SER-TAMPERED-999";
        string microtext = "ANY-TEXT";
        var scan = CreateSynthesizedFrame("[TAMPERED][DIFFRACTION:0.88][MICROTEXT:PASS]");

        var finding = _hologramVerifier.VerifyHologram(serial, scan, microtext);

        finding.Status.Should().Be(HologramVerificationStatus.TamperedDefaced);
    }

    [Fact]
    public void VerifyHologram_MarginalOptics_ReturnsIndeterminateOrCounterfeitSuspect()
    {
        string serial = "SER-MARGINAL-555";
        string microtext = "TEXT";
        var scan = CreateSynthesizedFrame("[DIFFRACTION:0.55][MICROTEXT:PASS]");

        var finding = _hologramVerifier.VerifyHologram(serial, scan, microtext);

        finding.Status.Should().Be(HologramVerificationStatus.Indeterminate);
    }

    #endregion

    #region 6. Anti-Counterfeiting: Blacklists, Emission Ledgers & Duplicate Clones

    [Fact]
    public void VerifyHologram_BlacklistedSerial_FlagsImmediatelyAsCounterfeitSuspect()
    {
        string blacklistedSerial = "SER-ROGUE-BLACKLISTED-001";
        _hologramVerifier.BlacklistSerial(blacklistedSerial);

        var scan = CreateSynthesizedFrame("[DIFFRACTION:0.95][MICROTEXT:PASS]");
        var finding = _hologramVerifier.VerifyHologram(blacklistedSerial, scan, "ANY-KEY");

        finding.Status.Should().Be(HologramVerificationStatus.CounterfeitSuspect);
    }

    [Fact]
    public void VerifyHologram_DuplicateSerialEmission_FlagsClonedHologramAsCounterfeitSuspect()
    {
        string serial = "SER-CLONE-TARGET-999";
        string microtext = "SGROWING-CLONE-CHECK";
        var scan = CreateSynthesizedFrame($"[DIFFRACTION:0.90][MICROTEXT:PASS]{microtext}");

        // First verification should pass as authentic
        var firstFinding = _hologramVerifier.VerifyHologram(serial, scan, microtext);
        firstFinding.Status.Should().Be(HologramVerificationStatus.AuthenticVerified);

        // Second verification with the exact same serial code is a clone attempt!
        var secondFinding = _hologramVerifier.VerifyHologram(serial, scan, microtext);
        secondFinding.Status.Should().Be(HologramVerificationStatus.CounterfeitSuspect);
    }

    [Fact]
    public void VerifyHologram_UnregisteredBatchSerial_FlagsAsCounterfeitSuspect()
    {
        var localVerifier = new CounterfeitHologramVerifierService();
        localVerifier.RegisterManufacturerBatch("SUPPLIER-ALPHA", ["VALID-001", "VALID-002"]);

        var scan = CreateSynthesizedFrame("[DIFFRACTION:0.95][MICROTEXT:PASS]");
        var finding = localVerifier.VerifyHologram("UNREGISTERED-SERIAL-999", scan, "KEY");

        finding.Status.Should().Be(HologramVerificationStatus.CounterfeitSuspect);
    }

    [Fact]
    public void RegisterManufacturerBatch_ValidSerials_RegistersSuccessfully()
    {
        string supplier = "SUPPLIER-BETA";
        string[] serials = ["BATCH-A-01", "BATCH-A-02", "BATCH-A-03"];

        _hologramVerifier.RegisterManufacturerBatch(supplier, serials);

        _hologramVerifier.IsSerialKnown("BATCH-A-01").Should().BeTrue();
        _hologramVerifier.IsSerialKnown("BATCH-A-02").Should().BeTrue();
        _hologramVerifier.IsSerialKnown("UNKNOWN-SERIAL").Should().BeFalse();
    }

    [Fact]
    public void VerifyHologram_NullOrEmptyInputs_ThrowsAppropriateExceptions()
    {
        var scan = CreateCleanFrame();

        var actNullSerial = () => _hologramVerifier.VerifyHologram(null!, scan, "KEY");
        actNullSerial.Should().Throw<ArgumentException>();

        var actEmptyScan = () => _hologramVerifier.VerifyHologram("SER-1", [], "KEY");
        actEmptyScan.Should().Throw<ArgumentException>();

        var actNullText = () => _hologramVerifier.VerifyHologram("SER-1", scan, null!);
        actNullText.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region 7. GRN Discrepancy Debit Memos, Bin Allocation & DZD Currency

    [Fact]
    public void ProcessInspectionDiscrepancy_WithDefects_CalculatesTotalDebitAmountDzd()
    {
        var inspection = new PackagingInspectionResult(
            InspectionId: "INSP-DEBIT-01",
            ConsignmentWaybill: "WB-DEBIT-01",
            TotalDefectsFound: 12,
            IntegrityScore: 40.0,
            Disposition: InspectionDisposition.RejectedReturnToVendor,
            Findings: [],
            InspectedAtUtc: DateTime.UtcNow);

        decimal unitPriceDzd = 4500.50m;
        var memo = _grnRouter.ProcessInspectionDiscrepancy("PO-2026-001", "SUPP-ALGIERS-01", inspection, unitPriceDzd);

        memo.DefectiveItemsCount.Should().Be(12);
        memo.TotalDebitAmountDzd.Should().Be(54006.00m); // 12 * 4500.50 = 54006.00 DZD
        memo.PurchaseOrderId.Should().Be("PO-2026-001");
        memo.SupplierId.Should().Be("SUPP-ALGIERS-01");
        memo.QuarantineBinLocation.Should().StartWith("QUAR-ZONE-");
    }

    [Fact]
    public void ProcessInspectionDiscrepancy_FormatsQuarantineBinCorrectly()
    {
        var inspection = new PackagingInspectionResult(
            InspectionId: "INSP-BIN-01",
            ConsignmentWaybill: "WB-BIN-01",
            TotalDefectsFound: 5,
            IntegrityScore: 65.0,
            Disposition: InspectionDisposition.QuarantinePendingReview,
            Findings: [],
            InspectedAtUtc: DateTime.UtcNow);

        var memo = _grnRouter.ProcessInspectionDiscrepancy("PO-BIN-100", "SUPP-BIN-01", inspection, 1200m);

        // Pattern: QUAR-ZONE-{Floor:D2}-{Bay} e.g. QUAR-ZONE-01-A01
        memo.QuarantineBinLocation.Should().MatchRegex(@"^QUAR-ZONE-\d{2}-[A-Z]\d{2}$");
    }

    [Fact]
    public void ProcessInspectionDiscrepancy_GeneratesValidCryptographicHmacSignature()
    {
        var inspection = new PackagingInspectionResult(
            InspectionId: "INSP-SIG-01",
            ConsignmentWaybill: "WB-SIG-01",
            TotalDefectsFound: 3,
            IntegrityScore: 55.0,
            Disposition: InspectionDisposition.RejectedReturnToVendor,
            Findings: [],
            InspectedAtUtc: DateTime.UtcNow);

        var memo = _grnRouter.ProcessInspectionDiscrepancy("PO-SIG-100", "SUPP-ORAN-02", inspection, 2500m);

        memo.CryptographicAuditSignature.Should().NotBeNullOrWhiteSpace();
        memo.CryptographicAuditSignature.Length.Should().Be(64); // SHA-256 hex string = 64 hex characters

        bool isValid = _grnRouter.VerifyDebitMemoSignature(memo);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyDebitMemoSignature_TamperedPayload_ReturnsFalse()
    {
        var inspection = new PackagingInspectionResult(
            InspectionId: "INSP-TAMPER-01",
            ConsignmentWaybill: "WB-TAMPER-01",
            TotalDefectsFound: 2,
            IntegrityScore: 70.0,
            Disposition: InspectionDisposition.QuarantinePendingReview,
            Findings: [],
            InspectedAtUtc: DateTime.UtcNow);

        var originalMemo = _grnRouter.ProcessInspectionDiscrepancy("PO-SEC-01", "SUPP-ANNABA-01", inspection, 3000m);

        // Tamper with debit amount
        var tamperedMemo = originalMemo with { TotalDebitAmountDzd = 999999.00m };
        bool isValid = _grnRouter.VerifyDebitMemoSignature(tamperedMemo);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void ProcessInspectionDiscrepancy_ZeroDefects_RoutesToDirectStockWithZeroDebit()
    {
        var inspection = new PackagingInspectionResult(
            InspectionId: "INSP-CLEAN-01",
            ConsignmentWaybill: "WB-CLEAN-01",
            TotalDefectsFound: 0,
            IntegrityScore: 100.0,
            Disposition: InspectionDisposition.AcceptedDirectToStock,
            Findings: [],
            InspectedAtUtc: DateTime.UtcNow);

        var memo = _grnRouter.ProcessInspectionDiscrepancy("PO-CLEAN-01", "SUPP-SETIF-01", inspection, 5000m);

        memo.DefectiveItemsCount.Should().Be(0);
        memo.TotalDebitAmountDzd.Should().Be(0m);
        memo.QuarantineBinLocation.Should().Be("NONE-DIRECT-TO-STOCK");
    }

    #endregion

    #region 8. Concurrency, Thread Safety & DI Registration

    [Fact]
    public void ListQuarantinedMemos_ReturnsOnlyQuarantinedMemos()
    {
        var localRouter = new AutonomousGrnDiscrepancyRouter();

        var defectiveInspection = new PackagingInspectionResult(
            "INSP-Q-1", "WB-Q-1", 4, 60.0, InspectionDisposition.QuarantinePendingReview, [], DateTime.UtcNow);
        var cleanInspection = new PackagingInspectionResult(
            "INSP-C-1", "WB-C-1", 0, 100.0, InspectionDisposition.AcceptedDirectToStock, [], DateTime.UtcNow);

        localRouter.ProcessInspectionDiscrepancy("PO-Q-1", "SUPP-1", defectiveInspection, 100m);
        localRouter.ProcessInspectionDiscrepancy("PO-C-1", "SUPP-1", cleanInspection, 100m);

        var quarantined = localRouter.ListQuarantinedMemos();
        quarantined.Should().HaveCount(1);
        quarantined[0].PurchaseOrderId.Should().Be("PO-Q-1");
    }

    [Fact]
    public void GetMemosByPurchaseOrderAndSupplier_FiltersCorrectly()
    {
        var inspection = new PackagingInspectionResult(
            "INSP-F-1", "WB-F-1", 2, 70.0, InspectionDisposition.QuarantinePendingReview, [], DateTime.UtcNow);

        _grnRouter.ProcessInspectionDiscrepancy("PO-FILTER-TARGET", "SUPP-FILTER-TARGET", inspection, 1500m);

        var poMemos = _grnRouter.GetMemosByPurchaseOrder("PO-FILTER-TARGET");
        poMemos.Should().NotBeEmpty();

        var suppMemos = _grnRouter.GetMemosBySupplier("SUPP-FILTER-TARGET");
        suppMemos.Should().NotBeEmpty();

        _grnRouter.GetMemosByPurchaseOrder("NON-EXISTENT-PO").Should().BeEmpty();
    }

    [Fact]
    public void VisualPackagingAnomalyDetector_ConcurrentInspections_IsCompletelyThreadSafe()
    {
        var detector = new VisualPackagingAnomalyDetector();
        int taskCount = 20;

        Parallel.For(0, taskCount, i =>
        {
            var frame = CreateSynthesizedFrame($"[DEFECT:CrushedBoxCorner:0.90:Moderate]");
            var res = detector.AnalyzePackagingImage($"WB-CONCURRENT-{i}", frame);
            res.Should().NotBeNull();
            res.TotalDefectsFound.Should().Be(1);
        });

        detector.GetAllInspections().Should().HaveCount(taskCount);
    }

    [Fact]
    public void CounterfeitHologramVerifier_ConcurrentCloningAttempts_DetectsDuplicatesSafely()
    {
        var verifier = new CounterfeitHologramVerifierService();
        string sharedSerial = "SER-CONCURRENT-SHARED-001";
        string microtext = "SGROWING-LOCK-CHECK";
        var scan = CreateSynthesizedFrame($"[DIFFRACTION:0.95][MICROTEXT:PASS]{microtext}");

        int authenticCount = 0;
        int counterfeitCount = 0;
        object countLock = new();

        Parallel.For(0, 10, _ =>
        {
            var finding = verifier.VerifyHologram(sharedSerial, scan, microtext);
            lock (countLock)
            {
                if (finding.Status == HologramVerificationStatus.AuthenticVerified)
                {
                    authenticCount++;
                }
                else if (finding.Status == HologramVerificationStatus.CounterfeitSuspect)
                {
                    counterfeitCount++;
                }
            }
        });

        // Exactly one should succeed as authentic, and all other 9 must be flagged as Counterfeit clones
        authenticCount.Should().Be(1);
        counterfeitCount.Should().Be(9);
    }

    [Fact]
    public void AddQualityGateServices_RegistersExpectedSingletons_WithoutCallingBuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddQualityGateServices();

        services.Should().Contain(d => d.ServiceType == typeof(IVisualPackagingAnomalyDetector) &&
                                       d.ImplementationType == typeof(VisualPackagingAnomalyDetector) &&
                                       d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(ICounterfeitHologramVerifierService) &&
                                       d.ImplementationType == typeof(CounterfeitHologramVerifierService) &&
                                       d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(IAutonomousGrnDiscrepancyRouter) &&
                                       d.ImplementationType == typeof(AutonomousGrnDiscrepancyRouter) &&
                                       d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
