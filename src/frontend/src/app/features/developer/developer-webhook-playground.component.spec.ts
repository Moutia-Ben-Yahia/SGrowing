// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import {
  DeveloperWebhookPlaygroundComponent,
  computeHmacSha256,
  SAMPLE_PAYLOADS,
  WebhookEventType
} from './developer-webhook-playground.component';

describe('DeveloperWebhookPlaygroundComponent (Interactive Webhook Playground & HMAC Verifier)', () => {
  let component: DeveloperWebhookPlaygroundComponent;

  beforeEach(() => {
    component = new DeveloperWebhookPlaygroundComponent();
  });

  describe('Initialization & Default State', () => {
    it('should create the component instance with expected default tabs and state', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab()).toBe('dispatcher');
      expect(component.selectedEvent()).toBe('order.placed');
      expect(component.targetUrl()).toBe('https://api.partner.io/webhooks/sgrowing');
      expect(component.signingSecret()).toContain('whsec_');
      expect(component.rateLimitTotal()).toBe(120);
      expect(component.rateLimitUsed()).toBe(42);
      expect(component.dlqHealthStatus()).toBe('HEALTHY');
      expect(component.apiScopes()).toEqual(['developer:read', 'developer:write', '*']);
    });

    it('should have pre-populated valid sample JSON for order.placed', () => {
      expect(component.isPayloadValidJson()).toBe(true);
      const parsed = JSON.parse(component.payloadJson());
      expect(parsed.eventType).toBe('order.placed');
      expect(parsed.order.currency).toBe('EUR');
    });

    it('should support tab navigation', () => {
      component.selectTab('verifier');
      expect(component.activeTab()).toBe('verifier');

      component.selectTab('snippets');
      expect(component.activeTab()).toBe('snippets');

      component.selectTab('ratelimit');
      expect(component.activeTab()).toBe('ratelimit');

      component.selectTab('dispatcher');
      expect(component.activeTab()).toBe('dispatcher');
    });
  });

  describe('HMAC-SHA256 Cryptographic Computation', () => {
    it('should compute exact RFC 2104 / standard HMAC-SHA256 hex digest', () => {
      const secret = 'whsec_test';
      const message = 'test-data';
      const expected = 'sha256=c6f7f09f1890d91cc615026ad49fcb642e239fb6ceebe0d967adf662c37890d4';

      const result = computeHmacSha256(secret, message);
      expect(result).toBe(expected);
    });

    it('should dynamically update computedSignaturePreview when secret or payload changes', () => {
      const initialSig = component.computedSignaturePreview();
      expect(initialSig).toMatch(/^sha256=[a-f0-9]{64}$/);

      component.signingSecret.set('whsec_different_secret_key');
      const updatedSig = component.computedSignaturePreview();
      expect(updatedSig).toMatch(/^sha256=[a-f0-9]{64}$/);
      expect(updatedSig).not.toBe(initialSig);
    });

    it('should generate a new random signing secret with whsec_ prefix upon regeneration', () => {
      const originalSecret = component.signingSecret();
      component.regenerateSecret();
      expect(component.signingSecret()).toMatch(/^whsec_[a-f0-9]{24}$/);
      expect(component.signingSecret()).not.toBe(originalSecret);
    });
  });

  describe('Event Type Switching & Payload Management', () => {
    const eventTypes: WebhookEventType[] = [
      'order.placed',
      'mission.completed',
      'lead.captured',
      'content.brief.generated',
      'payment.confirmed'
    ];

    it.each(eventTypes)('should update payload when switching to event "%s"', (eventType) => {
      component.onEventChange(eventType);
      expect(component.selectedEvent()).toBe(eventType);

      const parsed = JSON.parse(component.payloadJson());
      expect(parsed.eventType).toBe(eventType);
      expect(component.isPayloadValidJson()).toBe(true);
    });

    it('should detect invalid JSON and calculate byte count correctly', () => {
      component.onPayloadChange('{ invalid json syntax');
      expect(component.isPayloadValidJson()).toBe(false);

      component.onPayloadChange('{"hello": "world"}');
      expect(component.isPayloadValidJson()).toBe(true);
      expect(component.payloadByteCount()).toBe(18);
    });

    it('should reset payload to default sample upon user request', () => {
      component.onEventChange('mission.completed');
      component.onPayloadChange('{"custom": "tampered"}');
      expect(component.payloadJson()).toBe('{"custom": "tampered"}');

      component.resetPayloadToDefault();
      const parsed = JSON.parse(component.payloadJson());
      expect(parsed.eventType).toBe('mission.completed');
      expect(parsed.mission.agentName).toBe('Autonomous Growth Director');
    });
  });

  describe('Webhook Test Dispatch & Inspector Execution', () => {
    it('should dispatch test webhook and populate complete inspection results', () => {
      expect(component.dispatchResult()).toBeNull();
      const initialUsed = component.rateLimitUsed();

      component.dispatchTestWebhook();

      const result = component.dispatchResult();
      expect(result).not.toBeNull();
      expect(result!.statusCode).toBe(200);
      expect(result!.statusText).toBe('OK');
      expect(result!.latencyMs).toBeGreaterThan(0);
      expect(result!.deliveryId).toMatch(/^del_\d+_[a-z0-9]+$/);
      expect(result!.eventType).toBe('order.placed');
      expect(result!.signature).toBe(component.computedSignaturePreview());

      // Verify transmitted headers table
      const headerNames = result!.headers.map(h => h.name);
      expect(headerNames).toContain('X-SG-Signature');
      expect(headerNames).toContain('X-SG-Event');
      expect(headerNames).toContain('X-SG-Delivery-Id');
      expect(headerNames).toContain('X-SG-Timestamp');
      expect(headerNames).toContain('Content-Type');

      // Verify response body
      const parsedBody = JSON.parse(result!.responseBody);
      expect(parsedBody.received).toBe(true);
      expect(parsedBody.signatureVerified).toBe(true);

      // Verify rate limit consumption increment
      expect(component.rateLimitUsed()).toBe(initialUsed + 1);
    });

    it('should not dispatch if payload is invalid JSON', () => {
      component.onPayloadChange('{ bad json');
      component.dispatchTestWebhook();
      expect(component.dispatchResult()).toBeNull();
    });

    it('should load dispatch result directly into live signature verifier', () => {
      component.dispatchTestWebhook();
      const result = component.dispatchResult()!;

      component.loadResultIntoVerifier(result);

      expect(component.activeTab()).toBe('verifier');
      expect(component.verifierPayload()).toBe(result.requestPayload);
      expect(component.verifierIncomingSig()).toBe(result.signature);
      expect(component.verifierSecret()).toBe(component.signingSecret());
      expect(component.verifierStatus().state).toBe('MATCH');
    });
  });

  describe('Live Signature Verifier', () => {
    it('should verify matching HMAC-SHA256 signature successfully', () => {
      const secret = 'whsec_verifier_secret_99';
      const payload = JSON.stringify({ event: 'test.verified', data: 42 });
      const validSig = computeHmacSha256(secret, payload);

      component.verifierSecret.set(secret);
      component.verifierPayload.set(payload);
      component.verifierIncomingSig.set(validSig);

      const status = component.verifierStatus();
      expect(status.state).toBe('MATCH');
      expect(status.computedSignature).toBe(validSig);
    });

    it('should report MISMATCH when incoming signature is incorrect', () => {
      const secret = 'whsec_verifier_secret_99';
      const payload = JSON.stringify({ event: 'test.verified', data: 42 });

      component.verifierSecret.set(secret);
      component.verifierPayload.set(payload);
      component.verifierIncomingSig.set('sha256=0000000000000000000000000000000000000000000000000000000000000000');

      const status = component.verifierStatus();
      expect(status.state).toBe('MISMATCH');
    });

    it('should report MISMATCH when payload is altered after signing', () => {
      const secret = 'whsec_verifier_secret_99';
      const payload = JSON.stringify({ event: 'test.verified', amount: 100 });
      const validSig = computeHmacSha256(secret, payload);

      component.verifierSecret.set(secret);
      component.verifierPayload.set(JSON.stringify({ event: 'test.verified', amount: 9999 }));
      component.verifierIncomingSig.set(validSig);

      const status = component.verifierStatus();
      expect(status.state).toBe('MISMATCH');
    });

    it('should report WAITING state if secret or signature is missing', () => {
      component.verifierIncomingSig.set('');
      expect(component.verifierStatus().state).toBe('WAITING');

      component.verifierIncomingSig.set('sha256=123');
      component.verifierSecret.set('');
      expect(component.verifierStatus().state).toBe('WAITING');
    });

    it('should auto-format JSON in verifier payload', () => {
      component.verifierPayload.set('{"compact":"json","value":1}');
      component.formatVerifierJson();
      expect(component.verifierPayload()).toContain('\n');
      expect(JSON.parse(component.verifierPayload()).compact).toBe('json');
    });

    it('should import dispatcher settings directly into verifier', () => {
      component.onEventChange('payment.confirmed');
      component.loadDispatcherIntoVerifier();

      expect(component.verifierPayload()).toBe(component.payloadJson());
      expect(component.verifierSecret()).toBe(component.signingSecret());
      expect(component.verifierIncomingSig()).toBe(component.computedSignaturePreview());
      expect(component.verifierStatus().state).toBe('MATCH');
    });
  });

  describe('Multi-Language SDK Code Snippets', () => {
    it('should generate valid C# (.NET 10) snippet with HMACSHA256 and FixedTimeEquals', () => {
      component.snippetLanguage.set('csharp');
      const code = component.getActiveSnippetCode();
      const filename = component.getSnippetFilename();

      expect(filename).toContain('.NET 10');
      expect(code).toContain('using System.Security.Cryptography;');
      expect(code).toContain('HMACSHA256');
      expect(code).toContain('CryptographicOperations.FixedTimeEquals');
      expect(code).toContain('X-SG-Signature');
      expect(code).toContain('X-SG-Event');
    });

    it('should generate valid TypeScript / Node.js snippet with crypto.createHmac and timingSafeEqual', () => {
      component.snippetLanguage.set('typescript');
      const code = component.getActiveSnippetCode();
      const filename = component.getSnippetFilename();

      expect(filename).toContain('Node.js');
      expect(code).toContain("import crypto from 'node:crypto';");
      expect(code).toContain("crypto.createHmac('sha256', WEBHOOK_SECRET)");
      expect(code).toContain('crypto.timingSafeEqual');
      expect(code).toContain('x-sg-signature');
    });

    it('should generate valid Python snippet with hmac.new and compare_digest', () => {
      component.snippetLanguage.set('python');
      const code = component.getActiveSnippetCode();
      const filename = component.getSnippetFilename();

      expect(filename).toContain('Python 3.12');
      expect(code).toContain('import hmac');
      expect(code).toContain('import hashlib');
      expect(code).toContain('hmac.new(WEBHOOK_SECRET');
      expect(code).toContain('hmac.compare_digest(signature');
      expect(code).toContain('X-SG-Signature');
    });

    it('should generate valid cURL snippet with X-SG-Signature and API key ping', () => {
      component.snippetLanguage.set('curl');
      const code = component.getActiveSnippetCode();
      const filename = component.getSnippetFilename();

      expect(filename).toContain('cURL');
      expect(code).toContain('curl -X POST');
      expect(code).toContain('X-SG-Signature: sha256=');
      expect(code).toContain('X-SG-Event: order.placed');
      expect(code).toContain('/api/developer/v1/auth-test');
    });

    it('should trigger clipboard copy and update feedback status', () => {
      component.copyCurrentSnippet();
      expect(component.copiedSnippet()).toBe('activeSdk');
    });
  });

  describe('Rate Limiting & Quota Meter', () => {
    it('should simulate request consumption and update usage gauge', () => {
      const initialUsed = component.rateLimitUsed();
      component.simulateRequest(5);
      expect(component.rateLimitUsed()).toBe(initialUsed + 5);

      component.simulateRequest(10);
      expect(component.rateLimitUsed()).toBe(initialUsed + 15);
    });

    it('should cap rate limit consumption at maximum threshold', () => {
      component.simulateRequest(200);
      expect(component.rateLimitUsed()).toBe(120);
    });

    it('should reset window counter back to zero', () => {
      component.simulateRequest(50);
      expect(component.rateLimitUsed()).toBeGreaterThan(0);

      component.resetRateLimitCounter();
      expect(component.rateLimitUsed()).toBe(0);
      expect(component.resetCountdownSeconds()).toBe(60);
    });

    it('should compute DLQ health status correctly', () => {
      expect(component.dlqHealthStatus()).toBe('HEALTHY');

      component.dlqFailedCount.set(3);
      expect(component.dlqHealthStatus()).toBe('DEGRADED');

      component.dlqFailedCount.set(0);
      expect(component.dlqHealthStatus()).toBe('HEALTHY');
    });

    it('should provide reset epoch based on countdown', () => {
      const nowEpoch = Math.floor(Date.now() / 1000);
      const resetEpoch = component.currentResetEpoch();
      expect(resetEpoch).toBeGreaterThanOrEqual(nowEpoch + 27);
    });
  });
});
