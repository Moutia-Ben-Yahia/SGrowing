using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using SGrowing.Api.Modules.Gateways;
using Xunit;

namespace SGrowing.Tests.Gateways;

public class GatewayAdaptersTests
{
    [Fact]
    public void WhatsAppCloudApiAdapter_VerifyWebhookSignature_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var adapter = new WhatsAppCloudApiAdapter(new HttpClient());
        var secret = "meta_app_secret_test_12345";
        var payload = "{\"entry\":[{\"id\":\"123\",\"changes\":[]}]}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var header = $"sha256={hash}";

        // Act
        var isValid = adapter.VerifyWebhookSignature(payload, header, secret);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void WhatsAppCloudApiAdapter_VerifyWebhookSignature_TamperedPayload_ReturnsFalse()
    {
        // Arrange
        var adapter = new WhatsAppCloudApiAdapter(new HttpClient());
        var secret = "meta_app_secret_test_12345";
        var payload = "{\"entry\":[{\"id\":\"123\",\"changes\":[]}]}";
        var tamperedPayload = "{\"entry\":[{\"id\":\"999\",\"changes\":[]}]}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var header = $"sha256={hash}";

        // Act
        var isValid = adapter.VerifyWebhookSignature(tamperedPayload, header, secret);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task WhatsAppCloudApiAdapter_SendTemplateMessage_MockToken_ReturnsAcceptedResult()
    {
        // Arrange
        var adapter = new WhatsAppCloudApiAdapter(new HttpClient());

        // Act
        var result = await adapter.SendTemplateMessageAsync(
            phoneNumberId: "phone_123",
            accessToken: "mock_token_xyz",
            recipientPhone: "+213555019283",
            templateName: "order_confirmation_v1",
            languageCode: "en_US",
            parameters: new Dictionary<string, string> { { "1", "ORD-994" } }
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("accepted", result.Status);
        Assert.Equal("+213555019283", result.RecipientPhone);
        Assert.StartsWith("WAMID-", result.MessageId);
    }

    [Fact]
    public void LiveKitMediaServerAdapter_GenerateWebRtcRoomToken_ProducesValidJwtWithRoomClaim()
    {
        // Arrange
        var adapter = new LiveKitMediaServerAdapter();
        var apiKey = "LKAPI_TEST_KEY_1234";
        var apiSecret = "super_secret_signing_key_at_least_32_characters_long!";
        var roomName = "webrtc_room_deal_992";
        var participant = "agent_karim";

        // Act
        var token = adapter.GenerateWebRtcRoomToken(roomName, participant, apiKey, apiSecret, TimeSpan.FromHours(2));

        // Assert
        Assert.NotNull(token);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(apiKey, jwt.Issuer);
        Assert.Equal(participant, jwt.Subject);
        Assert.Contains("video", jwt.Claims.Select(c => c.Type));
    }

    [Fact]
    public async Task SmtpSocketClientAdapter_ProbeNonExistentHost_ReturnsUnreachableGracefully()
    {
        // Arrange
        var adapter = new SmtpSocketClientAdapter();

        // Act
        var result = await adapter.ProbeSmtpServerAsync("invalid.nonexistent.domain.example.com", 587);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsReachable);
        Assert.NotNull(result.ErrorMessage);
    }
}
