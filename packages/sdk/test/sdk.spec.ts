import test from 'node:test';
import assert from 'node:assert';
import { SGrowingClient, computeWebhookSignature, verifyWebhookSignature } from '../src/index.js';

test('SGrowingClient initialization throws when apiKey or tenantId missing', () => {
  assert.throws(() => new SGrowingClient({ apiKey: '', tenantId: '123' }), /apiKey/);
  assert.throws(() => new SGrowingClient({ apiKey: 'sg_live_abc', tenantId: '' }), /tenantId/);

  const client = new SGrowingClient({
    apiKey: 'sg_live_key123',
    tenantId: '00000000-0000-0000-0000-000000000001',
    baseUrl: 'https://api.sgrowing.io'
  });

  assert.ok(client.contacts);
  assert.ok(client.deals);
  assert.ok(client.billing);
  assert.ok(client.webhooks);
  assert.ok(client.telemetry);
  assert.ok(client.agents);
});

test('Webhook HMAC-SHA256 signature compute and verify', () => {
  const payload = JSON.stringify({
    eventType: 'order.delivered',
    orderId: 'ORD-9901',
    amount: 145.5
  });
  const secret = 'whsec_test_secret_key_8899';
  const timestamp = Math.floor(Date.now() / 1000);

  const signature = computeWebhookSignature(payload, secret, timestamp);
  assert.match(signature, /^sha256=[a-f0-9]{64}$/);

  const isValid = verifyWebhookSignature({
    payload,
    signature,
    secret,
    timestamp
  });
  assert.strictEqual(isValid, true);

  const isInvalid = verifyWebhookSignature({
    payload,
    signature: 'sha256=0000000000000000000000000000000000000000000000000000000000000000',
    secret,
    timestamp
  });
  assert.strictEqual(isInvalid, false);

  // Expired timestamp test
  const isExpired = verifyWebhookSignature({
    payload,
    signature,
    secret,
    timestamp: timestamp - 600, // 10 minutes ago
    toleranceSeconds: 300
  });
  assert.strictEqual(isExpired, false);

  // Missing signature or secret returns false
  assert.strictEqual(verifyWebhookSignature({ payload, signature: '', secret, timestamp }), false);
  assert.strictEqual(verifyWebhookSignature({ payload, signature, secret: '', timestamp }), false);
  assert.strictEqual(verifyWebhookSignature({ payload, signature, secret, timestamp: 'invalid_ts' }), false);
});

test('SGrowingClient supports custom timeout and configuration defaults', () => {
  const client = new SGrowingClient({
    apiKey: 'sg_live_custom',
    tenantId: '00000000-0000-0000-0000-000000000002',
    baseUrl: 'https://custom-gateway.sgrowing.io',
    timeoutMs: 15000
  });

  assert.ok(client.contacts);
  assert.ok(client.deals);
  assert.ok(client.billing);
  assert.ok(client.webhooks);
  assert.ok(client.telemetry);
  assert.ok(client.agents);
});
