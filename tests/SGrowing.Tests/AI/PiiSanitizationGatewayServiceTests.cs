using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Modules.AI.Firewall;
using Xunit;

namespace SGrowing.Tests.AI;

public class PiiSanitizationGatewayServiceTests
{
    private readonly PiiSanitizationGatewayService _sanitizer;

    public PiiSanitizationGatewayServiceTests()
    {
        var mockLogger = new Mock<ILogger<PiiSanitizationGatewayService>>();
        _sanitizer = new PiiSanitizationGatewayService(mockLogger.Object);
    }

    [Theory]
    [InlineData("4532015112830366", true)]  // Valid 16-digit Visa
    [InlineData("5424180123456789", true)]  // Valid 16-digit MasterCard
    [InlineData("4532015112830367", false)] // Invalid check digit
    [InlineData("1234567890123456", false)] // Invalid 16-digit sequence
    public void IsValidLuhn_ValidatesCorrectly(string candidate, bool expected)
    {
        PiiSanitizationGatewayService.IsValidLuhn(candidate).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_WhenNoPii_ReturnsOriginalText()
    {
        var input = "The quick brown fox jumps over the lazy dog in Algiers.";

        var result = _sanitizer.Sanitize(input);

        result.ContainsPii.Should().BeFalse();
        result.SanitizedText.Should().Be(input);
        result.RedactedEntities.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_CreditCard_RedactsWithCardMarker()
    {
        var input = "Payment processed using card 4532-0151-1283-0366 for order 992.";

        var result = _sanitizer.Sanitize(input);

        result.ContainsPii.Should().BeTrue();
        result.SanitizedText.Should().Contain("[REDACTED_CREDIT_CARD]");
        result.SanitizedText.Should().NotContain("4532-0151-1283-0366");
        result.RedactedTypes.Should().Contain("CreditCard");
    }

    [Fact]
    public void Sanitize_UsSsn_RedactsWithSsnMarker()
    {
        var input = "Employee tax ID record: SSN is 123-45-6789 on file.";

        var result = _sanitizer.Sanitize(input);

        result.ContainsPii.Should().BeTrue();
        result.SanitizedText.Should().Contain("[REDACTED_SSN]");
        result.SanitizedText.Should().NotContain("123-45-6789");
        result.RedactedTypes.Should().Contain("SSN");
    }

    [Fact]
    public void Sanitize_AlgerianNin_Redacts18DigitIdentifier()
    {
        var algerianNin = "123456789012345678"; // 18 digits
        var input = $"Customer national identity number: {algerianNin} registered in Oran.";

        var result = _sanitizer.Sanitize(input);

        result.ContainsPii.Should().BeTrue();
        result.SanitizedText.Should().Contain("[REDACTED_ALGERIAN_NIN]");
        result.SanitizedText.Should().NotContain(algerianNin);
        result.RedactedTypes.Should().Contain("AlgerianNIN");
    }

    [Fact]
    public void Sanitize_FrenchNir_RedactsSocialSecurityNumber()
    {
        var frenchNir = "1 85 12 75 108 042 12";
        var input = $"Assuré social numéro NIR: {frenchNir} pour remboursement.";

        var result = _sanitizer.Sanitize(input);

        result.ContainsPii.Should().BeTrue();
        result.SanitizedText.Should().Contain("[REDACTED_FRENCH_NIR]");
        result.SanitizedText.Should().NotContain("1 85 12 75 108 042 12");
        result.RedactedTypes.Should().Contain("FrenchNIR");
    }

    [Fact]
    public void Sanitize_KnownApiKeys_RedactsStripeAndOpenAiTokens()
    {
        var openAiKey = "sk-proj-abc123def456ghi789jkl012mno345pqr";
        var stripeKey = "sk_live_1234567890abcdef1234567890abcdef";
        var input = $"Connection credentials: {openAiKey} and payment key {stripeKey}";

        var result = _sanitizer.Sanitize(input);

        result.ContainsPii.Should().BeTrue();
        result.SanitizedText.Should().Contain("[REDACTED_API_KEY]");
        result.SanitizedText.Should().NotContain(openAiKey);
        result.SanitizedText.Should().NotContain(stripeKey);
        result.RedactedTypes.Should().Contain("ApiKey");
    }

    [Fact]
    public void Sanitize_KeyValuePasswords_RedactsPlaintextSecrets()
    {
        var input = "Database credentials: host=db.internal; user=admin; password=SuperSecretPassword123!; port=5432";

        var result = _sanitizer.Sanitize(input);

        result.ContainsPii.Should().BeTrue();
        result.SanitizedText.Should().Contain("[REDACTED_PASSWORD]");
        result.SanitizedText.Should().NotContain("SuperSecretPassword123!");
        result.RedactedTypes.Should().Contain("Password");
    }

    [Fact]
    public void ContainsPii_ReturnsBooleanReflectingSanitizationState()
    {
        _sanitizer.ContainsPii("Safe text with no secrets.").Should().BeFalse();
        _sanitizer.ContainsPii("Secret key api_key: sk-proj-123456789012345678901234").Should().BeTrue();
    }
}
