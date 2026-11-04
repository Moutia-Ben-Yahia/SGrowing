using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
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
        session.Status.Should().Be("connected");
        session.QrCodeBase64.Should().NotBeNull();
        sendResult.Status.Should().Be("SENT");
        sendResult.RecipientPhone.Should().Be("+21622334455");
    }

    [Fact]
    public async Task WhatsAppGateway_SendInteractiveButtons_ProducesCorrectButtonMessageId()
    {
        // Act
        var result = await _waClient.SendInteractiveButtonsAsync(
            "instance-algiers-02",
            "+213555123456",
            "Please confirm your delivery window",
            new List<string> { "Morning (9am-1pm)", "Afternoon (2pm-6pm)" }
        );

        // Assert
        result.Status.Should().Be("SENT");
        result.RecipientPhone.Should().Be("+213555123456");
        result.MessageId.Should().StartWith("EVO-BTN-");
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
        parsed.EventType.Should().Be("messages.upsert");
        parsed.SenderPhone.Should().Be("21622334455");
        parsed.SelectedOptionId.Should().Be("CONFIRM_ORDER");
    }

    [Fact]
    public async Task WhatsAppGateway_ParseWebhookPayload_ExtractsPlainTextConversation()
    {
        // Arrange
        var json = """
        {
            "event": "messages.upsert",
            "data": {
                "key": { "remoteJid": "21698765432@s.whatsapp.net" },
                "message": {
                    "conversation": "Can I change my delivery address to Sousse?"
                }
            }
        }
        """;
        var doc = JsonDocument.Parse(json);

        // Act
        var parsed = await _waClient.ParseWebhookPayloadAsync(doc.RootElement);

        // Assert
        parsed.SenderPhone.Should().Be("21698765432");
        parsed.Text.Should().Be("Can I change my delivery address to Sousse?");
        parsed.SelectedOptionId.Should().BeNull();
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
        yalidineLabel.TrackingNumber.Should().StartWith("YAL-");
        yalidineLabel.EstimatedShippingFee.Should().Be(7.5m);

        cathedisLabel.TrackingNumber.Should().StartWith("CATH-");
        cathedisLabel.EstimatedShippingFee.Should().Be(8.0m);
    }

    [Fact]
    public async Task CourierGateway_GenerateLabel_SupportsDhlExpressRateAndPrefix()
    {
        // Arrange
        var req = new CourierLabelRequestDto(
            "ORD-9002",
            "Amine Belhadj",
            "+213555778899",
            "Boulevard Mohamed V",
            "Algiers",
            "Algiers",
            450.0m,
            "DZD",
            2.0m
        );

        // Act
        var dhlLabel = await _courierGateway.GenerateShipmentLabelAsync("DHL Express Worldwide", req);

        // Assert
        dhlLabel.TrackingNumber.Should().StartWith("DHL-");
        dhlLabel.EstimatedShippingFee.Should().Be(24.0m);
        dhlLabel.LabelPdfUrl.Should().Contain(dhlLabel.TrackingNumber);
    }

    [Fact]
    public async Task CourierGateway_GenerateLabel_FallbackToStandardRateForUnknownProvider()
    {
        // Arrange
        var req = new CourierLabelRequestDto(
            "ORD-9003",
            "Local Shopper",
            "+21650000000",
            "Rue de la Republique",
            "Sfax",
            "Sfax",
            85.0m,
            "TND",
            0.5m
        );

        // Act
        var genericLabel = await _courierGateway.GenerateShipmentLabelAsync("Regional Speed Courier", req);

        // Assert
        genericLabel.TrackingNumber.Should().StartWith("TRK-");
        genericLabel.EstimatedShippingFee.Should().Be(8.5m);
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
        normalized.NormalizedStatus.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task CourierGateway_TranslateWebhook_HandlesDeliveredAndCollectedCash()
    {
        // Arrange
        var payload = new CourierTrackingWebhookPayloadDto(
            "YAL-261104-123456",
            "completed",
            "Oran Delivery Center",
            320.50m,
            null,
            DateTime.UtcNow
        );

        // Act
        var result = await _courierGateway.TranslateWebhookEventAsync("Yalidine Fast Delivery", payload);

        // Assert
        result.NormalizedStatus.Should().Be(CourierShipmentStatus.Delivered);
        result.Message.Should().Contain("Oran Delivery Center");
        result.Message.Should().Contain("320.50");
    }
}
