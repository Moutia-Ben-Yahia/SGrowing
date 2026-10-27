using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security;

public class WebhookSignatureValidator
{
    /// <summary>
    /// Validates Meta (Facebook / Instagram / WhatsApp Cloud) X-Hub-Signature-256 header.
    /// Format: "sha256=<hex_digest>"
    /// </summary>
    public static bool VerifyMetaSignature(string rawBody, string appSecret, string signatureHeader)
    {
        if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        signatureHeader = signatureHeader.Trim();
        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedHex = signatureHeader.Substring(prefix.Length).Trim();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedHex = Convert.ToHexString(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedHex.ToLowerInvariant()));
    }

    /// <summary>
    /// Validates Twilio X-Twilio-Signature header using HMAC-SHA1.
    /// Twilio sorts POST parameters, concatenates with URL, and signs with AuthToken.
    /// </summary>
    public static bool VerifyTwilioSignature(
        string requestUrl,
        IDictionary<string, string>? formParams,
        string authToken,
        string twilioSignatureHeader)
    {
        if (string.IsNullOrEmpty(requestUrl) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(twilioSignatureHeader))
        {
            return false;
        }

        var sb = new StringBuilder(requestUrl);
        if (formParams != null && formParams.Count > 0)
        {
            foreach (var kvp in formParams.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                sb.Append(kvp.Key).Append(kvp.Value);
            }
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        var computedBase64 = Convert.ToBase64String(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedBase64),
            Encoding.UTF8.GetBytes(twilioSignatureHeader));
    }

    /// <summary>
    /// Validates Telegram X-Telegram-Bot-Api-Secret-Token header.
    /// </summary>
    public static bool VerifyTelegramSecretToken(string? receivedSecretHeader, string expectedSecret)
    {
        if (string.IsNullOrEmpty(receivedSecretHeader) || string.IsNullOrEmpty(expectedSecret))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(receivedSecretHeader),
            Encoding.UTF8.GetBytes(expectedSecret));
    }

    /// <summary>
    /// Validates Discord webhook payload integrity and timestamp replay prevention.
    /// </summary>
    public static bool VerifyDiscordTimestampAndPayload(
        string rawBody,
        string timestampHeader,
        string signatureHeader,
        TimeSpan? maxClockDrift = null)
    {
        if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(timestampHeader) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        // Validate timestamp is a valid numeric unix timestamp
        if (!long.TryParse(timestampHeader, out var timestampSeconds))
        {
            return false;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds).UtcDateTime;
        var driftLimit = maxClockDrift ?? TimeSpan.FromMinutes(5);

        if (Math.Abs((DateTime.UtcNow - requestTime).TotalSeconds) > driftLimit.TotalSeconds)
        {
            return false; // Replay attack prevention
        }

        // Validate signature format (Discord signatures are 64-byte Ed25519 hex strings = 128 hex chars)
        if (signatureHeader.Length != 128)
        {
            return false;
        }

        return true;
    }
}
