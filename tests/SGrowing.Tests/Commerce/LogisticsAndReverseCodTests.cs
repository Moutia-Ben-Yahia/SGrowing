using FluentAssertions;
using SGrowing.Api.Modules.Commerce.Logistics;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class LogisticsAndReverseCodTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    #region 1. COD Fraud Predictor & High-Risk Gating Tests

    [Fact]
    public async Task EvaluateRisk_WithValidAddressAndNormalOrder_ShouldAssignLowRiskAndAutoApprove()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: Guid.NewGuid(),
            PhoneNumber: "+21620123456",
            DeliveryAddress: "14 Avenue Habib Bourguiba, Apt 3B",
            CustomerName: "Amine Ben Ali",
            DestinationCity: "Tunis",
            OrderAmount: 120.0m,
            TotalPastOrders: 3,
            PastRefusalsCount: 0,
            PastUncollectedCount: 0,
            SuccessfulDeliveriesCount: 3);

        // Act
        var result = await service.EvaluateRiskAsync(_tenantId, request);

        // Assert
        result.RiskScore.Should().BeLessThan(30);
        result.Tier.Should().Be(FraudRiskTier.Low);
        result.Status.Should().Be(CodDeliveryVerificationStatus.AutoApproved);
        result.VerificationCode.Should().BeNull();
        result.VerificationCodeExpiresAtUtc.Should().BeNull();
        result.Factors.Should().Contain(f => f.FactorCode == "TRUSTED_BUYER_BONUS");
    }

    [Fact]
    public async Task EvaluateRisk_WithLowAddressEntropy_ShouldIncreaseScoreAndFlagLowEntropy()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: Guid.NewGuid(),
            PhoneNumber: "+21622334455",
            DeliveryAddress: "aaaa bbbb aaaa bbbb aaaa", // repetitive keyboard mash with low entropy
            CustomerName: "Test Buyer",
            DestinationCity: "Tunis",
            OrderAmount: 80.0m);

        // Act
        var result = await service.EvaluateRiskAsync(_tenantId, request);

        // Assert
        result.Factors.Should().Contain(f => f.FactorCode == "LOW_ADDRESS_ENTROPY");
        result.RiskScore.Should().BeGreaterThanOrEqualTo(30);
    }

    [Fact]
    public async Task EvaluateRisk_WithGibberishAddress_ShouldDetectGibberishPattern()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: Guid.NewGuid(),
            PhoneNumber: "+21698765432",
            DeliveryAddress: "Street asdf asdf test test 123",
            CustomerName: "John Doe",
            DestinationCity: "Tunis",
            OrderAmount: 95.0m);

        // Act
        var result = await service.EvaluateRiskAsync(_tenantId, request);

        // Assert
        result.Factors.Should().Contain(f => f.FactorCode == "GIBBERISH_ADDRESS_DETECTED");
        result.RiskScore.Should().BeGreaterThanOrEqualTo(35);
    }

    [Fact]
    public async Task EvaluateRisk_WithHighPhoneRefusalRate_ShouldFlagCriticalRefusalRisk()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: Guid.NewGuid(),
            PhoneNumber: "+21655667788",
            DeliveryAddress: "Rue de Marseille 12",
            CustomerName: "Kamel Riahi",
            DestinationCity: "Tunis",
            OrderAmount: 150.0m,
            TotalPastOrders: 10,
            PastRefusalsCount: 6); // 60% refusal rate

        // Act
        var result = await service.EvaluateRiskAsync(_tenantId, request);

        // Assert
        result.Factors.Should().Contain(f => f.FactorCode == "HIGH_PHONE_REFUSAL_RATE");
        result.RiskScore.Should().BeGreaterThanOrEqualTo(40);
    }

    [Fact]
    public async Task EvaluateRisk_WithMultipleUncollectedOrders_ShouldAccumulateUncollectedPenalties()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: Guid.NewGuid(),
            PhoneNumber: "+21650112233",
            DeliveryAddress: "Rue de Carthage 45",
            CustomerName: "Mounir",
            DestinationCity: "Sfax",
            OrderAmount: 180.0m,
            PastUncollectedCount: 3); // 3 * 15 = 45 points

        // Act
        var result = await service.EvaluateRiskAsync(_tenantId, request);

        // Assert
        result.Factors.Should().Contain(f => f.FactorCode == "PREVIOUS_UNCOLLECTED_ORDERS");
        result.Factors.First(f => f.FactorCode == "PREVIOUS_UNCOLLECTED_ORDERS").ScorePoints.Should().Be(45);
    }

    [Fact]
    public async Task EvaluateRisk_WithHighOrderValue_ShouldAddHighExposurePenalty()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: Guid.NewGuid(),
            PhoneNumber: "+21620111222",
            DeliveryAddress: "Les Berges du Lac 2, Villa 14",
            CustomerName: "Karim",
            DestinationCity: "Tunis",
            OrderAmount: 850.0m); // > 500

        // Act
        var result = await service.EvaluateRiskAsync(_tenantId, request);

        // Assert
        result.Factors.Should().Contain(f => f.FactorCode == "HIGH_ORDER_VALUE_THRESHOLD");
        result.Factors.First(f => f.FactorCode == "HIGH_ORDER_VALUE_THRESHOLD").ScorePoints.Should().Be(25);
    }

    [Fact]
    public async Task EvaluateRisk_WhenScoreExceeds65_ShouldAssignHighRiskAndGenerateOtpToken()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var orderId = Guid.NewGuid();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: orderId,
            PhoneNumber: "1111111111", // Repeated dummy digits (+35)
            DeliveryAddress: "asdf asdf test test", // Gibberish (+35)
            CustomerName: "Anonymous",
            DestinationCity: "", // Missing city (+20)
            OrderAmount: 600.0m, // High value (+25)
            TotalPastOrders: 5,
            PastRefusalsCount: 4, // High refusal (+40)
            PastUncollectedCount: 2); // Uncollected (+30)

        // Act
        var result = await service.EvaluateRiskAsync(_tenantId, request);

        // Assert
        result.RiskScore.Should().Be(100);
        result.Tier.Should().Be(FraudRiskTier.High);
        result.Status.Should().Be(CodDeliveryVerificationStatus.HighRiskPendingCall);
        result.VerificationCode.Should().NotBeNullOrWhiteSpace();
        result.VerificationCode!.Length.Should().Be(6);
        result.VerificationCodeExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);

        var record = await service.GetVerificationStatusAsync(_tenantId, orderId);
        record.Should().NotBeNull();
        record!.VerificationCode.Should().Be(result.VerificationCode);
    }

    [Fact]
    public async Task VerifyOrder_WithCorrectCode_ShouldVerifyOrderAndLiftHold()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var orderId = Guid.NewGuid();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: orderId,
            PhoneNumber: "0000000000",
            DeliveryAddress: "test test asdf",
            OrderAmount: 700.0m);

        var assessment = await service.EvaluateRiskAsync(_tenantId, request);
        assessment.Status.Should().Be(CodDeliveryVerificationStatus.HighRiskPendingCall);

        // Act
        var verifyResult = await service.VerifyOrderAsync(_tenantId, orderId, assessment.VerificationCode!, "WhatsApp");

        // Assert
        verifyResult.Success.Should().BeTrue();
        verifyResult.Status.Should().Be(CodDeliveryVerificationStatus.VerifiedByCustomer);
        verifyResult.Channel.Should().Be("WhatsApp");
        verifyResult.VerifiedAtUtc.Should().NotBeNull();

        var record = await service.GetVerificationStatusAsync(_tenantId, orderId);
        record!.Status.Should().Be(CodDeliveryVerificationStatus.VerifiedByCustomer);
        record.VerifiedChannel.Should().Be("WhatsApp");
    }

    [Fact]
    public async Task VerifyOrder_WithIncorrectCode_ShouldDecrementAttemptsAndFailOnMaxAttempts()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var orderId = Guid.NewGuid();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: orderId,
            PhoneNumber: "0000000000",
            DeliveryAddress: "test test asdf",
            OrderAmount: 700.0m);

        var assessment = await service.EvaluateRiskAsync(_tenantId, request);

        // Act & Assert 1: First wrong attempt
        var res1 = await service.VerifyOrderAsync(_tenantId, orderId, "999999", "IVR");
        res1.Success.Should().BeFalse();
        res1.Message.Should().Contain("2 attempts remaining");

        // Act & Assert 2: Second wrong attempt
        var res2 = await service.VerifyOrderAsync(_tenantId, orderId, "888888", "IVR");
        res2.Success.Should().BeFalse();
        res2.Message.Should().Contain("1 attempts remaining");

        // Act & Assert 3: Third wrong attempt triggers RejectedFraud
        var res3 = await service.VerifyOrderAsync(_tenantId, orderId, "777777", "IVR");
        res3.Success.Should().BeFalse();
        res3.Status.Should().Be(CodDeliveryVerificationStatus.RejectedFraud);
        res3.Message.Should().Contain("Maximum verification attempts exceeded");
    }

    [Fact]
    public async Task VerifyOrder_WithExpiredCode_ShouldFailVerification()
    {
        // Arrange
        var service = new CodFraudPredictorService();
        var orderId = Guid.NewGuid();
        var request = new CodFraudEvaluationRequest(
            TenantId: _tenantId,
            OrderId: orderId,
            PhoneNumber: "0000000000",
            DeliveryAddress: "test test asdf",
            OrderAmount: 700.0m);

        var assessment = await service.EvaluateRiskAsync(_tenantId, request);
        var record = await service.GetVerificationStatusAsync(_tenantId, orderId);
        record!.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5); // Expired

        // Act
        var result = await service.VerifyOrderAsync(_tenantId, orderId, assessment.VerificationCode!, "WhatsApp");

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be(CodDeliveryVerificationStatus.FailedVerification);
        result.Message.Should().Contain("expired");
    }

    #endregion

    #region 2. Carrier Smart Router & 3PL Rate Sheets Tests

    [Fact]
    public async Task RouteOrder_WithStandardPackage_ShouldCalculateTariffAndCodFeesAcrossCarriers()
    {
        // Arrange
        var router = new CarrierSmartRouterService();
        var orderId = Guid.NewGuid();

        // Act: 1.5 kg package to Algiers with $100 COD and 3-day SLA
        var recommendation = await router.RouteOrderAsync(_tenantId, orderId, "Algiers", weightKg: 1.5m, codAmount: 100m, maxSlaDays: 3);

        // Assert
        recommendation.OptimalCarrier.Should().NotBeNull();
        recommendation.OptimalCarrier.TariffCost.Should().BeGreaterThan(0m);
        recommendation.OptimalCarrier.CodCollectionFee.Should().BeGreaterThan(0m);
        recommendation.OptimalCarrier.TotalShippingCost.Should().Be(recommendation.OptimalCarrier.TariffCost + recommendation.OptimalCarrier.CodCollectionFee);
        recommendation.FallbackRanking.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RouteOrder_WithRemoteCity_ShouldApplyRemoteZoneSurcharge()
    {
        // Arrange
        var router = new CarrierSmartRouterService();
        var orderId = Guid.NewGuid();

        // Act: Route to remote city "Tamanrasset"
        var remoteRec = await router.RouteOrderAsync(_tenantId, orderId, "Tamanrasset", weightKg: 1.0m, codAmount: 50m, maxSlaDays: 5);

        // Find Yalidine quote
        var allQuotes = new List<CarrierQuote> { remoteRec.OptimalCarrier };
        allQuotes.AddRange(remoteRec.FallbackRanking);
        var yalidineQuote = allQuotes.FirstOrDefault(q => q.CourierName == "Yalidine");

        // Assert
        yalidineQuote.Should().NotBeNull();
        // BaseTariff (6.50) + RemoteSurcharge (2.50) = 9.00
        yalidineQuote!.TariffCost.Should().Be(9.00m);
    }

    [Fact]
    public async Task RouteOrder_WithTightSla_ShouldPrioritizeSlaCompliantCarriers()
    {
        // Arrange
        var router = new CarrierSmartRouterService();
        var orderId = Guid.NewGuid();

        // Act: Request 1-day SLA in Tunis
        var recommendation = await router.RouteOrderAsync(_tenantId, orderId, "Tunis", weightKg: 1.0m, codAmount: 200m, maxSlaDays: 1);

        // Assert
        recommendation.OptimalCarrier.MeetsSla.Should().BeTrue();
        recommendation.OptimalCarrier.EstimatedTransitDays.Should().BeLessThanOrEqualTo(1.0m);
        recommendation.OptimalCarrier.OnTimeDeliveryProbability.Should().BeGreaterThanOrEqualTo(0.90m);
    }

    [Fact]
    public async Task RouteOrder_ShouldProduceFallbackRankingOfAllViableCarriers()
    {
        // Arrange
        var router = new CarrierSmartRouterService();
        var orderId = Guid.NewGuid();

        // Act
        var rec = await router.RouteOrderAsync(_tenantId, orderId, "Tunis", weightKg: 2.0m, codAmount: 150m, maxSlaDays: 4);

        // Assert: 5 default carriers total (1 optimal + 4 fallbacks)
        rec.FallbackRanking.Count.Should().Be(4);
        var carrierNames = new List<string> { rec.OptimalCarrier.CourierName };
        carrierNames.AddRange(rec.FallbackRanking.Select(f => f.CourierName));

        carrierNames.Should().Contain(["Yalidine", "Aramex", "DHL", "FedEx", "LocalExpress"]);
    }

    [Fact]
    public async Task UpsertRateSheet_ShouldAllowDynamicCarrierOverride()
    {
        // Arrange
        var router = new CarrierSmartRouterService();
        var customSheet = new CarrierRateSheet
        {
            CourierName = "SpeedyDrop",
            BaseWeightKg = 2.0m,
            BaseTariff = 3.00m, // Extremely competitive
            ExtraKgTariff = 0.50m,
            CodFeePercentage = 0.5m,
            MinCodFee = 0.50m,
            HistoricalDeliverySuccessRate = 0.99m,
            HistoricalReturnCost = 2.00m,
            DefaultAvgTransitDays = 1.0m
        };

        // Act
        router.UpsertRateSheet(_tenantId, customSheet);
        var rec = await router.RouteOrderAsync(_tenantId, Guid.NewGuid(), "Tunis", weightKg: 1.0m, codAmount: 200m, maxSlaDays: 2);

        // Assert: Custom sheet with highest net yield and lowest tariff becomes optimal
        rec.OptimalCarrier.CourierName.Should().Be("SpeedyDrop");
        rec.OptimalCarrier.TariffCost.Should().Be(3.00m);
    }

    #endregion

    #region 3. Reverse Swap Delivery & Warehouse Quarantine Tests

    [Fact]
    public async Task InitiateReverseExchange_ShouldGenerateDualLinkedWaybills_WithProperPrefixes()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var request = new ReverseExchangeInitiationRequest(
            TenantId: _tenantId,
            OriginalOrderId: Guid.NewGuid(),
            CustomerName: "Fatma Zohra",
            CustomerPhone: "+21623456789",
            DeliveryAddress: "Rue de Paris 88",
            City: "Sousse",
            OriginalSku: "SHOE-BLACK-42",
            OriginalSkuPrice: 120.0m,
            ReplacementSku: "SHOE-BLACK-43",
            ReplacementSkuPrice: 120.0m,
            ExchangeReason: "WrongSize",
            CourierName: "Aramex");

        // Act
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, request);

        // Assert
        manifest.Should().NotBeNull();
        manifest.Waybills.Should().NotBeNull();
        manifest.Waybills.ForwardWaybillNumber.Should().StartWith("WB-FWD-");
        manifest.Waybills.ReverseWaybillNumber.Should().StartWith("WB-REV-");
        manifest.Waybills.LinkedExchangeId.Should().Be(manifest.ExchangeId);
        manifest.Status.Should().Be(ReverseExchangeStatus.DispatchedForDoorstepSwap);
        manifest.CustomerOwesDifference.Should().BeFalse();
        manifest.AmountDueFromCustomer.Should().Be(0m);
        manifest.RefundDueToCustomer.Should().Be(0m);
    }

    [Fact]
    public async Task InitiateReverseExchange_WhenReplacementIsMoreExpensive_CustomerOwesDifference()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var request = new ReverseExchangeInitiationRequest(
            TenantId: _tenantId,
            OriginalOrderId: Guid.NewGuid(),
            CustomerName: "Sami",
            CustomerPhone: "+21650111222",
            DeliveryAddress: "Avenue Bourguiba",
            City: "Tunis",
            OriginalSku: "HOODIE-BASIC-M",
            OriginalSkuPrice: 80.0m,
            ReplacementSku: "HOODIE-PREMIUM-L",
            ReplacementSkuPrice: 110.0m,
            ExchangeReason: "UpgradeModel");

        // Act
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, request);

        // Assert
        manifest.CashDifference.Should().Be(30.0m);
        manifest.CustomerOwesDifference.Should().BeTrue();
        manifest.AmountDueFromCustomer.Should().Be(30.0m);
        manifest.RefundDueToCustomer.Should().Be(0m);
    }

    [Fact]
    public async Task InitiateReverseExchange_WhenReplacementIsCheaper_RefundDueToCustomer()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var request = new ReverseExchangeInitiationRequest(
            TenantId: _tenantId,
            OriginalOrderId: Guid.NewGuid(),
            CustomerName: "Nadia",
            CustomerPhone: "+21698111222",
            DeliveryAddress: "Centre Urbain Nord",
            City: "Tunis",
            OriginalSku: "JACKET-LEATHER-L",
            OriginalSkuPrice: 200.0m,
            ReplacementSku: "JACKET-DENIM-M",
            ReplacementSkuPrice: 150.0m,
            ExchangeReason: "StylePreference");

        // Act
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, request);

        // Assert
        manifest.CashDifference.Should().Be(-50.0m);
        manifest.CustomerOwesDifference.Should().BeFalse();
        manifest.AmountDueFromCustomer.Should().Be(0m);
        manifest.RefundDueToCustomer.Should().Be(50.0m);
        manifest.RefundStatus.Should().Be(RefundProcessingStatus.PendingDoorstepInspection);
    }

    [Fact]
    public async Task ExecuteDoorstepSwap_WithoutScanningReverseWaybill_ShouldThrowException()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, new ReverseExchangeInitiationRequest(
            _tenantId, Guid.NewGuid(), "Farid", "+21622334455", "Rue d'Alger", "Tunis", "SKU-A", 50m, "SKU-B", 50m, "Fit"));

        var swapRequest = new DoorstepSwapRequest(
            TenantId: _tenantId,
            ExchangeId: manifest.ExchangeId,
            ScannedReverseWaybill: "WRONG-BARCODE-999",
            CashCollected: 0m,
            ReplacementHandedOver: true);

        // Act & Assert
        var act = async () => await service.ExecuteDoorstepSwapAsync(_tenantId, swapRequest);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Courier must scan and verify the valid return package waybill*");
    }

    [Fact]
    public async Task ExecuteDoorstepSwap_WhenCustomerOwesCashAndUnderpaid_ShouldThrowException()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, new ReverseExchangeInitiationRequest(
            _tenantId, Guid.NewGuid(), "Farid", "+21622334455", "Rue d'Alger", "Tunis", "SKU-A", 50m, "SKU-B", 90m, "Fit"));

        manifest.AmountDueFromCustomer.Should().Be(40m);

        var swapRequest = new DoorstepSwapRequest(
            TenantId: _tenantId,
            ExchangeId: manifest.ExchangeId,
            ScannedReverseWaybill: manifest.Waybills.ReverseWaybillNumber,
            CashCollected: 20m, // Underpayment: owes 40
            ReplacementHandedOver: true);

        // Act & Assert
        var act = async () => await service.ExecuteDoorstepSwapAsync(_tenantId, swapRequest);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Customer owes cash difference of*");
    }

    [Fact]
    public async Task ExecuteDoorstepSwap_WithValidScanAndPayment_ShouldCompleteDoorstepSwap()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, new ReverseExchangeInitiationRequest(
            _tenantId, Guid.NewGuid(), "Farid", "+21622334455", "Rue d'Alger", "Tunis", "SKU-A", 50m, "SKU-B", 75m, "Fit"));

        var swapRequest = new DoorstepSwapRequest(
            TenantId: _tenantId,
            ExchangeId: manifest.ExchangeId,
            ScannedReverseWaybill: manifest.Waybills.ReverseWaybillNumber,
            CashCollected: 25m,
            ReplacementHandedOver: true,
            CourierAgentId: "ARAMEX-COURIER-14");

        // Act
        var result = await service.ExecuteDoorstepSwapAsync(_tenantId, swapRequest);

        // Assert
        result.Status.Should().Be(ReverseExchangeStatus.SwapCompletedAtDoorstep);
        result.CashCollectedAtDoorstep.Should().Be(25m);
        result.HandoverConfirmed.Should().BeTrue();
        result.ReturnCollectedConfirmed.Should().BeTrue();
        result.DoorstepCompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessQuarantineInspection_PristineCondition_ShouldRestockAndReleaseRefund()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, new ReverseExchangeInitiationRequest(
            _tenantId, Guid.NewGuid(), "Ali", "+21622000111", "Lac 1", "Tunis", "WATCH-GOLD", 300m, "WATCH-SILVER", 250m, "Color"));

        await service.ExecuteDoorstepSwapAsync(_tenantId, new DoorstepSwapRequest(
            _tenantId, manifest.ExchangeId, manifest.Waybills.ReverseWaybillNumber, 0m, true));

        var inspection = new QuarantineInspectionRequest(
            TenantId: _tenantId,
            ExchangeId: manifest.ExchangeId,
            ScannedReturnBarcode: manifest.Waybills.ReverseWaybillNumber,
            Condition: QuarantineItemCondition.Pristine,
            InspectorNotes: "Box unopened with original security seal intact.",
            InspectorId: "QC-LEAD-01");

        // Act
        var result = await service.ProcessQuarantineInspectionAsync(_tenantId, inspection);

        // Assert
        result.Condition.Should().Be(QuarantineItemCondition.Pristine);
        result.NewStatus.Should().Be(ReverseExchangeStatus.CompletedPristine);
        result.RestockedToInventory.Should().BeTrue();
        result.RefundStatus.Should().Be(RefundProcessingStatus.ApprovedAndProcessed);
        result.RefundAmountIssued.Should().Be(50m);
        result.FraudIncidentLogged.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessQuarantineInspection_DamagedBoxCondition_ShouldApplyRepackagingDeduction()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, new ReverseExchangeInitiationRequest(
            _tenantId, Guid.NewGuid(), "Salma", "+21655112233", "Ennasr", "Tunis", "PERFUME-100ML", 100m, "PERFUME-50ML", 70m, "Size"));

        await service.ExecuteDoorstepSwapAsync(_tenantId, new DoorstepSwapRequest(
            _tenantId, manifest.ExchangeId, manifest.Waybills.ReverseWaybillNumber, 0m, true));

        var inspection = new QuarantineInspectionRequest(
            TenantId: _tenantId,
            ExchangeId: manifest.ExchangeId,
            ScannedReturnBarcode: manifest.Waybills.ReverseWaybillNumber,
            Condition: QuarantineItemCondition.DamagedBox,
            InspectorNotes: "Cardboard box crushed, bottle intact.",
            InspectorId: "QC-LEAD-02");

        // Act
        var result = await service.ProcessQuarantineInspectionAsync(_tenantId, inspection);

        // Assert
        result.Condition.Should().Be(QuarantineItemCondition.DamagedBox);
        result.NewStatus.Should().Be(ReverseExchangeStatus.CompletedDamagedBoxDiscount);
        result.RestockedToInventory.Should().BeTrue();
        result.RefundStatus.Should().Be(RefundProcessingStatus.ProcessedWithRepackagingDeduction);
        result.RefundAmountIssued.Should().Be(25m); // 30 refund - 5 packaging fee = 25
    }

    [Fact]
    public async Task ProcessQuarantineInspection_ItemDefectiveCondition_ShouldQuarantineForRma()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, new ReverseExchangeInitiationRequest(
            _tenantId, Guid.NewGuid(), "Hamza", "+21622338899", "Menzah", "Tunis", "HEADPHONES", 150m, "EARBUDS", 100m, "DefectivePort"));

        await service.ExecuteDoorstepSwapAsync(_tenantId, new DoorstepSwapRequest(
            _tenantId, manifest.ExchangeId, manifest.Waybills.ReverseWaybillNumber, 0m, true));

        var inspection = new QuarantineInspectionRequest(
            TenantId: _tenantId,
            ExchangeId: manifest.ExchangeId,
            ScannedReturnBarcode: manifest.Waybills.ReverseWaybillNumber,
            Condition: QuarantineItemCondition.ItemDefective,
            InspectorNotes: "Verified dead battery. Routed to RMA shelf.",
            InspectorId: "QC-TECH-03");

        // Act
        var result = await service.ProcessQuarantineInspectionAsync(_tenantId, inspection);

        // Assert
        result.Condition.Should().Be(QuarantineItemCondition.ItemDefective);
        result.NewStatus.Should().Be(ReverseExchangeStatus.CompletedDefectiveVerified);
        result.RestockedToInventory.Should().BeFalse(); // Not restocked to regular stock
        result.RefundStatus.Should().Be(RefundProcessingStatus.ApprovedAndProcessed);
        result.RefundAmountIssued.Should().Be(50m);
    }

    [Fact]
    public async Task ProcessQuarantineInspection_WrongItemReturned_ShouldTriggerFraudIncidentAndBlockRefund()
    {
        // Arrange
        var service = new ReverseExchangeService();
        var manifest = await service.InitiateReverseExchangeAsync(_tenantId, new ReverseExchangeInitiationRequest(
            _tenantId, Guid.NewGuid(), "ScamSuspect", "+21620000000", "Unknown Street", "Tunis", "SMARTPHONE-PRO", 800m, "SMARTPHONE-BASE", 600m, "Downsize"));

        await service.ExecuteDoorstepSwapAsync(_tenantId, new DoorstepSwapRequest(
            _tenantId, manifest.ExchangeId, manifest.Waybills.ReverseWaybillNumber, 0m, true));

        var inspection = new QuarantineInspectionRequest(
            TenantId: _tenantId,
            ExchangeId: manifest.ExchangeId,
            ScannedReturnBarcode: manifest.Waybills.ReverseWaybillNumber,
            Condition: QuarantineItemCondition.WrongItemReturned,
            InspectorNotes: "Parcel contained wooden block instead of returned smartphone.",
            InspectorId: "SECURITY-LEAD-07");

        // Act
        var result = await service.ProcessQuarantineInspectionAsync(_tenantId, inspection);

        // Assert
        result.Condition.Should().Be(QuarantineItemCondition.WrongItemReturned);
        result.NewStatus.Should().Be(ReverseExchangeStatus.QuarantinedFraudIncident);
        result.RestockedToInventory.Should().BeFalse();
        result.RefundStatus.Should().Be(RefundProcessingStatus.BlockedDueToFraud);
        result.RefundAmountIssued.Should().Be(0m);
        result.FraudIncidentLogged.Should().BeTrue();
        result.FraudIncident.Should().NotBeNull();
        result.FraudIncident!.Reason.Should().Contain("wooden block");
    }

    #endregion

    #region 4. Courier Remittance Ledger, Variance & Dispute Tests

    [Fact]
    public async Task RecordShipment_ShouldInitializeAtPendingCollectionStage()
    {
        // Arrange
        var service = new CourierRemittanceService();
        var waybill = "WB-ARX-1001";
        var orderId = Guid.NewGuid();

        // Act
        var entry = await service.RecordShipmentForCollectionAsync(_tenantId, waybill, orderId, "Aramex", 150m, 8m);

        // Assert
        entry.WaybillId.Should().Be(waybill);
        entry.OrderId.Should().Be(orderId);
        entry.CourierName.Should().Be("Aramex");
        entry.ExpectedCashAmount.Should().Be(150m);
        entry.ExpectedDeliveryFee.Should().Be(8m);
        entry.CurrentStage.Should().Be(RemittanceLedgerStage.PendingCollection);
        entry.IsDisputed.Should().BeFalse();
    }

    [Fact]
    public async Task TransitionStage_ValidSequentialFlow_ShouldAdvanceThroughAllStages()
    {
        // Arrange
        var service = new CourierRemittanceService();
        var waybill = "WB-YAL-2002";
        await service.RecordShipmentForCollectionAsync(_tenantId, waybill, Guid.NewGuid(), "Yalidine", 180m, 6.5m);

        // Act & Assert 1: PendingCollection -> CollectedByCourier
        var s1 = await service.TransitionStageAsync(_tenantId, waybill, RemittanceLedgerStage.CollectedByCourier, "Picked up from customer");
        s1.CurrentStage.Should().Be(RemittanceLedgerStage.CollectedByCourier);

        // Act & Assert 2: CollectedByCourier -> InCourierFloat
        var s2 = await service.TransitionStageAsync(_tenantId, waybill, RemittanceLedgerStage.InCourierFloat, "In regional hub cash vault");
        s2.CurrentStage.Should().Be(RemittanceLedgerStage.InCourierFloat);

        // Act & Assert 3: InCourierFloat -> RemittedToBank
        var s3 = await service.TransitionStageAsync(_tenantId, waybill, RemittanceLedgerStage.RemittedToBank, "Wire transfer batch BVR-441");
        s3.CurrentStage.Should().Be(RemittanceLedgerStage.RemittedToBank);

        // Act & Assert 4: RemittedToBank -> SettledWithMerchant
        var s4 = await service.TransitionStageAsync(_tenantId, waybill, RemittanceLedgerStage.SettledWithMerchant, "Funds released to tenant account");
        s4.CurrentStage.Should().Be(RemittanceLedgerStage.SettledWithMerchant);
    }

    [Fact]
    public async Task TransitionStage_InvalidSkip_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var service = new CourierRemittanceService();
        var waybill = "WB-DHL-3003";
        await service.RecordShipmentForCollectionAsync(_tenantId, waybill, Guid.NewGuid(), "DHL", 300m, 16m);

        // Act & Assert: Attempt to jump directly from PendingCollection to SettledWithMerchant
        var act = async () => await service.TransitionStageAsync(_tenantId, waybill, RemittanceLedgerStage.SettledWithMerchant);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid stage transition from PendingCollection to SettledWithMerchant*");
    }

    [Fact]
    public async Task ReconcileBatchStatement_ExactMatch_ShouldReconcileAndAdvanceStage()
    {
        // Arrange
        var service = new CourierRemittanceService();
        var waybill1 = "WB-RECON-01";
        var waybill2 = "WB-RECON-02";

        await service.RecordShipmentForCollectionAsync(_tenantId, waybill1, Guid.NewGuid(), "Aramex", 100m, 8m);
        await service.RecordShipmentForCollectionAsync(_tenantId, waybill2, Guid.NewGuid(), "Aramex", 150m, 8m);

        var batch = new CourierRemittanceBatchRequest(
            _tenantId,
            StatementReference: "STM-ARX-2026-W44",
            CourierName: "Aramex",
            StatementDateUtc: DateTime.UtcNow,
            Rows:
            [
                new CourierRemittanceBatchStatementRow(waybill1, CollectedCash: 100m, CourierFee: 8m),
                new CourierRemittanceBatchStatementRow(waybill2, CollectedCash: 150m, CourierFee: 8m)
            ]);

        // Act
        var report = await service.ReconcileBatchStatementAsync(_tenantId, batch);

        // Assert
        report.TotalWaybills.Should().Be(2);
        report.MatchedWaybills.Should().Be(2);
        report.DisputedWaybills.Should().Be(0);
        report.TotalCollectedCash.Should().Be(250m);
        report.TotalCourierFees.Should().Be(16m);
        report.NetRemittedCash.Should().Be(234m); // 250 - 16
        report.TotalVarianceAmount.Should().Be(0m);
        report.Disputes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileBatchStatement_CashShortfall_ShouldFlagCollectedCashShortfallDispute()
    {
        // Arrange
        var service = new CourierRemittanceService();
        var waybill = "WB-SHORTFALL-01";
        await service.RecordShipmentForCollectionAsync(_tenantId, waybill, Guid.NewGuid(), "LocalExpress", expectedCashAmount: 200m, expectedDeliveryFee: 5m);

        var batch = new CourierRemittanceBatchRequest(
            _tenantId,
            StatementReference: "STM-LEX-102",
            CourierName: "LocalExpress",
            StatementDateUtc: DateTime.UtcNow,
            Rows:
            [
                new CourierRemittanceBatchStatementRow(waybill, CollectedCash: 160m, CourierFee: 5m) // $40 shortfall
            ]);

        // Act
        var report = await service.ReconcileBatchStatementAsync(_tenantId, batch);

        // Assert
        report.DisputedWaybills.Should().Be(1);
        report.TotalVarianceAmount.Should().Be(40m);
        report.Disputes.Should().ContainSingle();
        var dispute = report.Disputes.First();
        dispute.DisputeType.Should().Be(RemittanceDisputeType.CollectedCashShortfall);
        dispute.Variance.Should().Be(40m);
        dispute.ExpectedAmount.Should().Be(200m);
        dispute.ReportedAmount.Should().Be(160m);

        var ledgerEntry = await service.GetLedgerEntryAsync(_tenantId, waybill);
        ledgerEntry!.IsDisputed.Should().BeTrue();
        ledgerEntry.DisputeType.Should().Be(RemittanceDisputeType.CollectedCashShortfall);
        ledgerEntry.VarianceAmount.Should().Be(40m);
    }

    [Fact]
    public async Task ReconcileBatchStatement_ExcessCourierFee_ShouldFlagFeeDiscrepancyDispute()
    {
        // Arrange
        var service = new CourierRemittanceService();
        var waybill = "WB-FEE-DISPUTE-01";
        await service.RecordShipmentForCollectionAsync(_tenantId, waybill, Guid.NewGuid(), "Yalidine", expectedCashAmount: 120m, expectedDeliveryFee: 6.5m);

        var batch = new CourierRemittanceBatchRequest(
            _tenantId,
            StatementReference: "STM-YAL-88",
            CourierName: "Yalidine",
            StatementDateUtc: DateTime.UtcNow,
            Rows:
            [
                new CourierRemittanceBatchStatementRow(waybill, CollectedCash: 120m, CourierFee: 11.5m) // $5 fee overcharge
            ]);

        // Act
        var report = await service.ReconcileBatchStatementAsync(_tenantId, batch);

        // Assert
        report.DisputedWaybills.Should().Be(1);
        report.TotalVarianceAmount.Should().Be(5.0m);
        var dispute = report.Disputes.First();
        dispute.DisputeType.Should().Be(RemittanceDisputeType.FeeDiscrepancy);
        dispute.Variance.Should().Be(5.0m);
    }

    [Fact]
    public async Task ReconcileBatchStatement_UnknownWaybill_ShouldFlagMissingPackageDispute()
    {
        // Arrange
        var service = new CourierRemittanceService();
        var unknownWaybill = "WB-PHANTOM-999";

        var batch = new CourierRemittanceBatchRequest(
            _tenantId,
            StatementReference: "STM-GHOST-01",
            CourierName: "FedEx",
            StatementDateUtc: DateTime.UtcNow,
            Rows:
            [
                new CourierRemittanceBatchStatementRow(unknownWaybill, CollectedCash: 95m, CourierFee: 14m)
            ]);

        // Act
        var report = await service.ReconcileBatchStatementAsync(_tenantId, batch);

        // Assert
        report.MatchedWaybills.Should().Be(0);
        report.DisputedWaybills.Should().Be(1);
        report.Disputes.Should().ContainSingle();
        var dispute = report.Disputes.First();
        dispute.DisputeType.Should().Be(RemittanceDisputeType.MissingPackageOrUnknownWaybill);
        dispute.Description.Should().Contain("was not found in system dispatched shipments");
    }

    [Fact]
    public async Task GetRemittanceSummary_ShouldAggregateCashAcrossAllStagesAndDisputes()
    {
        // Arrange
        var service = new CourierRemittanceService();

        // 1. Pending
        await service.RecordShipmentForCollectionAsync(_tenantId, "WB-S1", Guid.NewGuid(), "Aramex", 100m, 8m);

        // 2. Float
        await service.RecordShipmentForCollectionAsync(_tenantId, "WB-S2", Guid.NewGuid(), "Aramex", 200m, 8m);
        await service.TransitionStageAsync(_tenantId, "WB-S2", RemittanceLedgerStage.CollectedByCourier);
        await service.TransitionStageAsync(_tenantId, "WB-S2", RemittanceLedgerStage.InCourierFloat);

        // 3. Reconciled Remitted with dispute
        await service.RecordShipmentForCollectionAsync(_tenantId, "WB-S3", Guid.NewGuid(), "Aramex", 300m, 8m);
        await service.ReconcileBatchStatementAsync(_tenantId, new CourierRemittanceBatchRequest(
            _tenantId, "STM-SUMMARY", "Aramex", DateTime.UtcNow,
            [new CourierRemittanceBatchStatementRow("WB-S3", CollectedCash: 250m, CourierFee: 8m)])); // 50 shortfall

        // Act
        var summary = await service.GetRemittanceSummaryAsync(_tenantId, "Aramex");

        // Assert
        summary.TotalPendingCollection.Should().Be(100m);
        summary.TotalInCourierFloat.Should().Be(200m);
        summary.TotalRemittedToBank.Should().Be(250m);
        summary.ActiveDisputesCount.Should().Be(1);
        summary.TotalDisputedAmount.Should().Be(50m);
        summary.CashByStage["PendingCollection"].Should().Be(100m);
        summary.CashByStage["InCourierFloat"].Should().Be(200m);
    }

    #endregion
}
