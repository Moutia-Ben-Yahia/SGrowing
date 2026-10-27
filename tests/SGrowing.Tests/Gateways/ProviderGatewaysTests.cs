using System.Text.Json;
using SGrowing.Api.Modules.Gateways;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Gateways;

public class ProviderGatewaysTests
{
    private readonly EvolutionApiGatewayClient _waClient = new();
    private readonly CourierLogisticsGateway _courierGateway = new();

    [Fact]
    public async Task WhatsAppGateway_GetSessionStateAndSendMessage_Succeeds()
    {
        // Act
        var session = await _waClient.GetSessionStateAsync("instance-tunis-01");
        var sendResult = await _waClient.SendTextMessageAsync("instance-tunis-01", "+21622334455", "Hello from automated agent");

        // Assert
        Assert.Equal("connected", session.Status);
        Assert.NotNull(session.QrCodeBase64);
        Assert.Equal("SENT", sendResult.Status);
        Assert.Equal("+21622334455", sendResult.RecipientPhone);
    }

    [Fact]
    public async Task WhatsAppGateway_ParseWebhookPayload_ExtractsInteractiveReply()
    {
        // Arrange
        var json = """
        {
            "event": "messages.upsert",
            "data": {
                "key": { "remoteJid": "21622334455@s.whatsapp.net" },
                "message": {
                    "buttonsResponseMessage": {
                        "selectedButtonId": "CONFIRM_ORDER"
                    }
                }
            }
        }
        """;
        var doc = JsonDocument.Parse(json);

        // Act
        var parsed = await _waClient.ParseWebhookPayloadAsync(doc.RootElement);

        // Assert
        Assert.Equal("messages.upsert", parsed.EventType);
        Assert.Equal("21622334455", parsed.SenderPhone);
        Assert.Equal("CONFIRM_ORDER", parsed.SelectedOptionId);
    }

    [Fact]
    public async Task CourierGateway_GenerateLabel_ProducesStandardBarcodeAndFee()
    {
        // Arrange
        var req = new CourierLabelRequestDto(
            "ORD-9001",
            "Yassine Mansour",
            "+21622334455",
            "Avenue Habib Bourguiba",
            "Tunis",
            "Tunis",
            195.0m,
            "TND",
            1.5m
        );

        // Act
        var yalidineLabel = await _courierGateway.GenerateShipmentLabelAsync("Yalidine Fast Delivery", req);
        var cathedisLabel = await _courierGateway.GenerateShipmentLabelAsync("Cathedis Express", req);

        // Assert
        Assert.StartsWith("YAL-", yalidineLabel.TrackingNumber);
        Assert.Equal(7.5m, yalidineLabel.EstimatedShippingFee);

        Assert.StartsWith("CATH-", cathedisLabel.TrackingNumber);
        Assert.Equal(8.0m, cathedisLabel.EstimatedShippingFee);
    }

    [Theory]
    [InlineData("LIVRE - Client a recu le colis", CourierShipmentStatus.Delivered)]
    [InlineData("EN ROUTE / IN TRANSIT", CourierShipmentStatus.InTransit)]
    [InlineData("COLIS EN COURS DE DISTRIBUTION", CourierShipmentStatus.OutForDelivery)]
    [InlineData("DESTINATAIRE ABSENT / ECHEC", CourierShipmentStatus.FailedAttempt)]
    [InlineData("RETOUR A L'EXPEDITEUR - REFUS", CourierShipmentStatus.ReturnedToSender)]
    public async Task CourierGateway_TranslateWebhook_CorrectlyNormalizesStatuses(string rawStatus, CourierShipmentStatus expectedStatus)
    {
        // Arrange
        var payload = new CourierTrackingWebhookPayloadDto(
            "TRK-TEST-12345",
            rawStatus,
            "Casablanca Central Hub",
            120m,
            null,
            DateTime.UtcNow
        );

        // Act
        var normalized = await _courierGateway.TranslateWebhookEventAsync("Cathedis Express", payload);

        // Assert
        Assert.Equal(expectedStatus, normalized.NormalizedStatus);
    }
}
