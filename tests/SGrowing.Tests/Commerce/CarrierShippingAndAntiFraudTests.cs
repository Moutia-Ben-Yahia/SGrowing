using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.AntiFraud;
using SGrowing.Api.Modules.Commerce.Shipping;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CarrierShippingAndAntiFraudTests
{
    private readonly CarrierShippingGateway _shippingGateway = new();

    private (SGrowingDbContext db, CodAntiFraudEngine engine) CreateAntiFraudContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SGrowingDbContext(options);
        var engine = new CodAntiFraudEngine(db);
        return (db, engine);
    }

    [Fact]
    public async Task CarrierShippingGateway_GeneratesWaybillAndAwbBarcode_ForRegionalCouriers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var req = new WaybillGenerationRequest(
            CourierCode: "yalidine",
            OrderId: Guid.NewGuid(),
            OrderNumber: "ORD-9912",
            CustomerName: "Mourad Trabelsi",
            CustomerPhone: "+21698123456",
            DeliveryAddress: "Avenue Habib Bourguiba, Immeuble Etoile",
            DeliveryCity: "Tunis",
            DeliveryGovernorateOrState: "Tunis",
            CashToCollect: 165.0m,
            Currency: "TND",
            WeightKg: 1.5,
            PackageCount: 1,
            AllowCustomerToOpenBeforePaying: true,
            MerchantNotes: "Call customer prior to delivery"
        );

        // Act
        var waybill = await _shippingGateway.GenerateWaybillAsync(tenantId, req);

        // Assert
        Assert.NotNull(waybill);
        Assert.Equal("yalidine", waybill.CourierCode);
        Assert.Contains("Yalidine", waybill.CourierName);
        Assert.StartsWith("YAL-ORD-9912-", waybill.TrackingNumber);
        Assert.StartsWith("*YAL", waybill.AwbBarcode);
        Assert.Equal(165.0m, waybill.DeclaredCashToCollect);
        Assert.Equal(CarrierStatus.Created, waybill.Status);

        // Test Tracking
        var tracking = await _shippingGateway.TrackShipmentAsync(tenantId, "yalidine", waybill.TrackingNumber);
        Assert.NotEmpty(tracking.Checkpoints);
        Assert.Equal(CarrierStatus.OutForDelivery, tracking.CurrentStatus);

        // Test Label generation
        var label = await _shippingGateway.GenerateShippingLabelAsync(tenantId, "yalidine", waybill.TrackingNumber, LabelFormat.Zpl);
        Assert.NotNull(label.LabelContentBase64OrUrl);
    }

    [Fact]
    public async Task CodAntiFraudEngine_CleanOrder_EvaluatesToSafeTier()
    {
        // Arrange
        var (_, engine) = CreateAntiFraudContext();
        var tenantId = Guid.NewGuid();
        var req = new CodFraudEvaluationRequest(
            CustomerName: "Sami Mansour",
            CustomerPhone: "+21622345678",
            CustomerEmail: "sami.mansour@gmail.com",
            DeliveryAddress: "12 Rue Ibn Khaldoun, Ennasr 2",
            DeliveryCity: "Ariana",
            OrderTotal: 85.0m,
            ItemsCount: 2
        );

        // Act
        var result = await engine.EvaluateOrderRiskAsync(tenantId, req);

        // Assert
        Assert.Equal(CodRiskTier.Safe, result.Tier);
        Assert.Equal(CodFraudAction.AutoApprove, result.RecommendedAction);
        Assert.InRange(result.RiskScore, 0, 24);
        Assert.Empty(result.TriggeredRules);
    }

    [Fact]
    public async Task CodAntiFraudEngine_RepeatedDigitsAndGibberishAddress_FlagsFraudulentTier()
    {
        // Arrange
        var (_, engine) = CreateAntiFraudContext();
        var tenantId = Guid.NewGuid();
        var req = new CodFraudEvaluationRequest(
            CustomerName: "asdfasdf test",
            CustomerPhone: "+21699999999", // 8 repeated digits
            CustomerEmail: null,
            DeliveryAddress: "test test none fake street", // gibberish keywords
            DeliveryCity: "",
            OrderTotal: 650.0m, // high cash exposure
            ItemsCount: 15
        );

        // Act
        var result = await engine.EvaluateOrderRiskAsync(tenantId, req);

        // Assert
        Assert.Equal(CodRiskTier.Fraudulent, result.Tier);
        Assert.Equal(CodFraudAction.AutoReject, result.RecommendedAction);
        Assert.True(result.RiskScore >= 70);
        Assert.Contains(result.TriggeredRules, r => r.RuleId == "REPEATED_PHONE_DIGITS");
        Assert.Contains(result.TriggeredRules, r => r.RuleId == "GIBBERISH_KEYSTROKES");
    }

    [Fact]
    public async Task CodAntiFraudEngine_SerialRefusalsInHistory_TriggersCriticalSeverity()
    {
        // Arrange
        var (db, engine) = CreateAntiFraudContext();
        var tenantId = Guid.NewGuid();
        var customerPhone = "+21650112233";

        // Seed previous returned orders
        var returned1 = CodOrder.Create(tenantId, "ORD-HIST-01", "Mounir", customerPhone, "Address 1", "Tunis", "Tunis", 50m);
        returned1.Status = CodOrderStatus.Returned;
        var returned2 = CodOrder.Create(tenantId, "ORD-HIST-02", "Mounir", customerPhone, "Address 1", "Tunis", "Tunis", 60m);
        returned2.Status = CodOrderStatus.Returned;

        db.CodOrders.AddRange(returned1, returned2);
        await db.SaveChangesAsync();

        var req = new CodFraudEvaluationRequest(
            CustomerName: "Mounir",
            CustomerPhone: customerPhone,
            CustomerEmail: null,
            DeliveryAddress: "Rue de Carthage, 1000",
            DeliveryCity: "Tunis",
            OrderTotal: 70m,
            ItemsCount: 1
        );

        // Act
        var result = await engine.EvaluateOrderRiskAsync(tenantId, req);

        // Assert
        Assert.Contains(result.TriggeredRules, r => r.RuleId == "SERIAL_REFUSAL_HISTORY");
        Assert.True(result.RiskScore >= 45);
        Assert.True(result.Tier == CodRiskTier.HighRisk || result.Tier == CodRiskTier.Fraudulent);
    }

    [Fact]
    public async Task CarrierShippingGateway_CancelledWaybill_TracksAsCancelled()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var req = new WaybillGenerationRequest(
            CourierCode: "aramex",
            OrderId: Guid.NewGuid(),
            OrderNumber: "ORD-CNCL-01",
            CustomerName: "Walid Mansour",
            CustomerPhone: "+971501234567",
            DeliveryAddress: "Sheikh Zayed Road, Dubai",
            DeliveryCity: "Dubai",
            DeliveryGovernorateOrState: "Dubai",
            CashToCollect: 200m,
            Currency: "AED",
            WeightKg: 2.0,
            PackageCount: 1,
            AllowCustomerToOpenBeforePaying: false,
            MerchantNotes: null
        );

        var waybill = await _shippingGateway.GenerateWaybillAsync(tenantId, req);

        // Act: Cancel shipment
        var cancelResult = await _shippingGateway.CancelShipmentAsync(tenantId, "aramex", waybill.TrackingNumber);
        Assert.True(cancelResult.Success);

        // Assert: Tracking reflects Cancelled status, NOT OutForDelivery
        var tracking = await _shippingGateway.TrackShipmentAsync(tenantId, "aramex", waybill.TrackingNumber);
        Assert.Equal(CarrierStatus.Cancelled, tracking.CurrentStatus);
        Assert.Contains("cancelled", tracking.StatusDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CodAntiFraudEngine_ModerateRiskOrder_RecommendsSmsOtpConfirmation()
    {
        // Arrange: Missing destination city (+20 risk) + slightly short address (+0)
        var (_, engine) = CreateAntiFraudContext();
        var tenantId = Guid.NewGuid();
        var req = new CodFraudEvaluationRequest(
            CustomerName: "Karim Zied",
            CustomerPhone: "+21655123456",
            CustomerEmail: "karim@yahoo.fr",
            DeliveryAddress: "Rue Habib Bourguiba, Batiment B",
            DeliveryCity: "", // triggers MISSING_DESTINATION_CITY (20 points)
            OrderTotal: 300m,
            ItemsCount: 10 // triggers BULK_ITEM_QUANTITY (20 points)
        );

        // Act
        var result = await engine.EvaluateOrderRiskAsync(tenantId, req);

        // Assert
        Assert.True(result.RiskScore >= 25 && result.RiskScore < 45);
        Assert.Equal(CodRiskTier.Moderate, result.Tier);
        Assert.Equal(CodFraudAction.RequireSmsOtpConfirmation, result.RecommendedAction);
    }
}
