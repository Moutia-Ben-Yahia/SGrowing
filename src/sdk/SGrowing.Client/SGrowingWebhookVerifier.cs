using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Client;

/// <summary>
/// Provides timing-attack resilient cryptographic HMAC-SHA256 webhook signature validation
/// and clock-skew replay prevention.
/// </summary>
public static class SGrowingWebhookVerifier
{
    /// <summary>
    /// Default maximum acceptable clock skew between sender and receiver (300 seconds / 5 minutes).
    /// </summary>
    public const double DefaultToleranceSeconds = 300.0;

    /// <summary>
    /// Computes the HMAC-SHA256 signature for a payload.
    /// </summary>
    /// <param name="rawBody">The raw webhook request body payload.</param>
    /// <param name="secret">The shared secret key.</param>
    /// <param name="timestamp">Optional Unix timestamp to bind into the signature.</param>
    /// <returns>A lowercase hexadecimal hash prefixed with "sha256=".</returns>
    public static string ComputeSignature(string rawBody, string secret, long? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        ArgumentNullException.ThrowIfNull(secret);

        var dataToSign = timestamp.HasValue
            ? $"{timestamp.Value}.{rawBody}"
            : rawBody;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Validates an incoming webhook request signature in constant time.
    /// </summary>
    /// <param name="rawBody">The raw request body payload.</param>
    /// <param name="secret">The shared webhook secret.</param>
    /// <param name="signatureHeader">The signature header (e.g. from X-SG-Signature or X-SGrowing-Signature).</param>
    /// <param name="toleranceSeconds">Maximum allowed clock skew in seconds (default: 300).</param>
    /// <param name="timestampHeader">Optional explicit timestamp header value (Unix seconds or ISO string).</param>
    /// <returns>True if signature matches and is within the clock tolerance; otherwise false.</returns>
    public static bool Verify(
        string rawBody,
        string secret,
        string signatureHeader,
        double toleranceSeconds = DefaultToleranceSeconds,
        string? timestampHeader = null)
    {
        if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        long? parsedTimestamp = null;
        var expectedHex = signatureHeader.Trim();

        // Support structured header format: t=1731000000,v1=abcdef...
        if (signatureHeader.Contains("t=") && (signatureHeader.Contains("v1=") || signatureHeader.Contains("sha256=")))
        {
            var parts = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
                if (kv.Length == 2)
                {
                    if (string.Equals(kv[0], "t", StringComparison.OrdinalIgnoreCase) && long.TryParse(kv[1], out var t))
                    {
                        parsedTimestamp = t;
                    }
                    else if (string.Equals(kv[0], "v1", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(kv[0], "sha256", StringComparison.OrdinalIgnoreCase))
                    {
                        expectedHex = kv[1];
                    }
                }
            }
        }
        else if (expectedHex.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            expectedHex = expectedHex["sha256=".Length..].Trim();
        }
        else if (expectedHex.StartsWith("v1=", StringComparison.OrdinalIgnoreCase))
        {
            expectedHex = expectedHex["v1=".Length..].Trim();
        }

        // Support explicit timestamp header if provided
        if (!string.IsNullOrWhiteSpace(timestampHeader))
        {
            if (long.TryParse(timestampHeader.Trim(), out var explicitTs))
            {
                parsedTimestamp = explicitTs;
            }
            else if (DateTimeOffset.TryParse(timestampHeader.Trim(), out var parsedDto))
            {
                parsedTimestamp = parsedDto.ToUnixTimeSeconds();
            }
        }

        // Validate replay / drift window if timestamp was specified
        if (parsedTimestamp.HasValue)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - parsedTimestamp.Value) > toleranceSeconds)
            {
                return false;
            }
        }

        byte[] expectedBytes;
        try
        {
            expectedBytes = Convert.FromHexString(expectedHex);
        }
        catch
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var candidates = new List<byte[]>();

        if (parsedTimestamp.HasValue)
        {
            var boundData = $"{parsedTimestamp.Value}.{rawBody}";
            candidates.Add(hmac.ComputeHash(Encoding.UTF8.GetBytes(boundData)));
        }

        candidates.Add(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)));

        foreach (var candidate in candidates)
        {
            if (CryptographicOperations.FixedTimeEquals(candidate, expectedBytes))
            {
                return true;
            }
        }

        return false;
    }
}
