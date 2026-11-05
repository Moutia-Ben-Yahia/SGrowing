using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SGrowing.Api.Common.Security;

/// <summary>
/// SOC 2 Type II & OWASP ASVS Content Security Policy (CSP) Middleware.
/// Injects cryptographic per-request nonces and defensive HTTP security headers.
/// </summary>
public class CspNonceMiddleware
{
    public const string NonceItemKey = "CspNonce";
    public const string NonceHeaderKey = "X-Content-Security-Policy-Nonce";

    private readonly RequestDelegate _next;

    public CspNonceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate 128-bit cryptographically secure random nonce
        var nonceBytes = new byte[16];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = Convert.ToBase64String(nonceBytes);

        context.Items[NonceItemKey] = nonce;

        var headers = context.Response.Headers;

        if (!headers.ContainsKey(NonceHeaderKey))
        {
            headers[NonceHeaderKey] = nonce;
        }

        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            headers["Content-Security-Policy"] =
                $"default-src 'self'; script-src 'self' 'nonce-{nonce}' 'strict-dynamic'; " +
                $"style-src 'self' 'nonce-{nonce}' 'unsafe-inline'; img-src 'self' data: https:; " +
                $"font-src 'self' data:; connect-src 'self' https: wss:; frame-ancestors 'none'; " +
                $"object-src 'none'; base-uri 'self'; form-action 'self';";
        }

        if (!headers.ContainsKey("X-Content-Type-Options"))
        {
            headers["X-Content-Type-Options"] = "nosniff";
        }

        if (!headers.ContainsKey("X-Frame-Options"))
        {
            headers["X-Frame-Options"] = "DENY";
        }

        if (!headers.ContainsKey("Referrer-Policy"))
        {
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        }

        if (!headers.ContainsKey("Permissions-Policy"))
        {
            headers["Permissions-Policy"] = "camera=(), microphone=(self), geolocation=()";
        }

        await _next(context);
    }
}

public static class CspNonceExtensions
{
    private static readonly Regex ScriptTagRegex = new(@"<script(?![^>]*\bnonce=)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? GetCspNonce(this HttpContext? context)
    {
        if (context == null) return null;
        return context.Items.TryGetValue(CspNonceMiddleware.NonceItemKey, out var val) && val is string s
            ? s
            : null;
    }

    public static string InjectCspNonce(string html, string nonce)
    {
        if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(nonce)) return html;

        // Replace template placeholder if present
        var result = html.Replace("<!-- CSP_NONCE -->", nonce);

        // Inject nonce into unadorned <script> tags
        result = ScriptTagRegex.Replace(result, $"<script nonce=\"{nonce}\"");
        return result;
    }

    public static IApplicationBuilder UseCspNonceSecurity(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CspNonceMiddleware>();
    }
}
