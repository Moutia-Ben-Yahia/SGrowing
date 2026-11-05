using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SGrowing.Api.Common.Security;
using Xunit;

namespace SGrowing.Tests.Unit;

public class CspNonceMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_InjectsCryptographicNonceAndSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        bool nextInvoked = false;
        var middleware = new CspNonceMiddleware(ctx =>
        {
            nextInvoked = true;
            // Verify context.Items contains nonce during execution
            var itemNonce = ctx.Items[CspNonceMiddleware.NonceItemKey];
            Assert.NotNull(itemNonce);
            Assert.True(itemNonce.ToString()!.Length >= 20); // 16 bytes base64 is 24 chars
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextInvoked);

        // Trigger OnStarting callbacks
        await context.Response.StartAsync();

        var headers = context.Response.Headers;
        var nonce = context.GetCspNonce();

        Assert.NotNull(nonce);
        Assert.Equal(nonce, headers[CspNonceMiddleware.NonceHeaderKey]);

        // Check Content-Security-Policy
        Assert.True(headers.ContainsKey("Content-Security-Policy"));
        var csp = headers["Content-Security-Policy"].ToString();
        Assert.Contains($"'nonce-{nonce}'", csp);
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);

        // Check OWASP ASVS Headers
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
        Assert.Contains("microphone=(self)", headers["Permissions-Policy"].ToString());
    }

    [Fact]
    public void InjectCspNonce_ReplacesPlaceholderAndScriptTags()
    {
        var html = @"<!DOCTYPE html><html><head><meta name=""csp-nonce"" content=""<!-- CSP_NONCE -->""></head><body><script src=""app.js""></script></body></html>";
        var nonce = "kL89+AbC123/xyz==";

        var transformed = CspNonceExtensions.InjectCspNonce(html, nonce);

        Assert.Contains($"content=\"{nonce}\"", transformed);
        Assert.Contains($"<script nonce=\"{nonce}\" src=\"app.js\">", transformed);
        Assert.DoesNotContain("<!-- CSP_NONCE -->", transformed);
    }
}
