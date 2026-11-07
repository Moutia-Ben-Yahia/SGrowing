import { describe, it } from 'node:test';
import * as assert from 'node:assert/strict';
import {
  SGrowingClient,
  SGrowingWebhookVerifier,
  SGrowingApiError,
  type AgentMissionPlan,
  type MissionObjectiveRequest,
  type SreHealthRadarReport,
  type TransformPayloadResult
} from '../src/index.js';

describe('SGrowing TypeScript SDK', () => {
  describe('SGrowingWebhookVerifier', () => {
    const secret = 'whsec_test_secret_key_12345';
    const payload = JSON.stringify({ event: 'order.created', orderId: 'ORD-987' });

    it('should verify direct HMAC-SHA256 signature', () => {
      const sig = SGrowingWebhookVerifier.computeSignature(payload, secret);
      assert.ok(sig.startsWith('sha256='));

      const isValid = SGrowingWebhookVerifier.verify(payload, secret, sig);
      assert.equal(isValid, true);
    });

    it('should verify signature with timestamp header within tolerance', () => {
      const now = Math.floor(Date.now() / 1000);
      const sig = SGrowingWebhookVerifier.computeSignature(payload, secret, now);

      const isValid = SGrowingWebhookVerifier.verify(payload, secret, sig, 300, now);
      assert.equal(isValid, true);
    });

    it('should verify signature with t=...,v1=... header format', () => {
      const now = Math.floor(Date.now() / 1000);
      const hmacHex = SGrowingWebhookVerifier.computeSignature(payload, secret, now).replace('sha256=', '');
      const header = `t=${now},v1=${hmacHex}`;

      const isValid = SGrowingWebhookVerifier.verify(payload, secret, header, { toleranceSeconds: 300 });
      assert.equal(isValid, true);
    });

    it('should reject tampered payload', () => {
      const sig = SGrowingWebhookVerifier.computeSignature(payload, secret);
      const tamperedPayload = JSON.stringify({ event: 'order.created', orderId: 'ORD-999-HACKED' });

      const isValid = SGrowingWebhookVerifier.verify(tamperedPayload, secret, sig);
      assert.equal(isValid, false);
    });

    it('should reject signature signed with wrong secret', () => {
      const sig = SGrowingWebhookVerifier.computeSignature(payload, 'wrong_secret');
      const isValid = SGrowingWebhookVerifier.verify(payload, secret, sig);
      assert.equal(isValid, false);
    });

    it('should reject timestamp older than tolerance window (clock skew / replay)', () => {
      const expiredTimestamp = Math.floor(Date.now() / 1000) - 600; // 10 minutes ago
      const sig = SGrowingWebhookVerifier.computeSignature(payload, secret, expiredTimestamp);

      // Tolerance is 300 seconds (5 mins)
      const isValid = SGrowingWebhookVerifier.verify(payload, secret, sig, 300, expiredTimestamp);
      assert.equal(isValid, false);
    });

    it('should support Buffer payloads', () => {
      const buffer = Buffer.from(payload, 'utf-8');
      const sig = SGrowingWebhookVerifier.computeSignature(buffer, secret);

      const isValid = SGrowingWebhookVerifier.verify(buffer, secret, sig);
      assert.equal(isValid, true);
    });
  });

  describe('SGrowingClient', () => {
    const mockPlan: AgentMissionPlan = {
      missionId: '8f7a61d1-6b45-4df3-a602-550be0e527d9',
      title: 'Q4 Black Friday Surge',
      strategicGoal: 'Maximize ROAS and reactivate churned customers',
      status: 'InProgress',
      estimatedCost: 1500,
      createdAtUtc: new Date().toISOString(),
      taskDag: [
        {
          taskId: 'task-1',
          title: 'Audience Segmentation',
          agentRole: 'AudienceAnalyst',
          dependsOnTaskIds: [],
          status: 'Succeeded',
          requiresHumanApproval: false
        },
        {
          taskId: 'task-2',
          title: 'Ad Budget Allocation Approval',
          agentRole: 'CampaignStrategist',
          dependsOnTaskIds: ['task-1'],
          status: 'WaitingApproval',
          requiresHumanApproval: true
        }
      ]
    };

    it('should initialize with provided options and default headers', () => {
      const client = new SGrowingClient({
        apiKey: 'sg_live_testkey123',
        tenantId: '11111111-1111-1111-1111-111111111111',
        baseUrl: 'https://custom.sgrowing.io'
      });

      assert.equal(client.baseUrl, 'https://custom.sgrowing.io');
      assert.equal(client.apiKey, 'sg_live_testkey123');
      assert.equal(client.tenantId, '11111111-1111-1111-1111-111111111111');
      assert.equal(client.timeoutMs, 30000);
    });

    it('should call missions.plan and deserialize response correctly', async () => {
      let capturedUrl = '';
      let capturedMethod = '';
      let capturedBody = '';
      let capturedAuth = '';

      const mockFetch: typeof fetch = async (input, init) => {
        capturedUrl = input.toString();
        capturedMethod = init?.method ?? 'GET';
        capturedBody = init?.body?.toString() ?? '';
        const headers = init?.headers as Record<string, string>;
        capturedAuth = headers?.['Authorization'] ?? '';

        return new Response(JSON.stringify(mockPlan), {
          status: 201,
          headers: { 'Content-Type': 'application/json' }
        });
      };

      const client = new SGrowingClient({
        apiKey: 'sg_live_key',
        tenantId: 'tenant-123',
        baseUrl: 'https://api.test',
        fetch: mockFetch
      });

      const request: MissionObjectiveRequest = {
        title: 'Q4 Black Friday Surge',
        strategicGoal: 'Maximize ROAS',
        budgetLimit: 5000,
        targetGeo: 'US,UK'
      };

      const plan = await client.missions.plan(request);

      assert.equal(capturedUrl, 'https://api.test/api/v1/ai/missions/plan');
      assert.equal(capturedMethod, 'POST');
      assert.equal(capturedAuth, 'Bearer sg_live_key');
      assert.ok(capturedBody.includes('Q4 Black Friday Surge'));
      assert.equal(plan.missionId, mockPlan.missionId);
      assert.equal(plan.taskDag.length, 2);
    });

    it('should call missions.step without approveTaskId', async () => {
      let capturedUrl = '';

      const mockFetch: typeof fetch = async (input) => {
        capturedUrl = input.toString();
        return new Response(JSON.stringify(mockPlan), { status: 200 });
      };

      const client = new SGrowingClient({
        baseUrl: 'https://api.test',
        fetch: mockFetch
      });

      await client.missions.step('8f7a61d1-6b45-4df3-a602-550be0e527d9');
      assert.equal(capturedUrl, 'https://api.test/api/v1/ai/missions/8f7a61d1-6b45-4df3-a602-550be0e527d9/step');
    });

    it('should call missions.step with approveTaskId', async () => {
      let capturedUrl = '';

      const mockFetch: typeof fetch = async (input) => {
        capturedUrl = input.toString();
        return new Response(JSON.stringify(mockPlan), { status: 200 });
      };

      const client = new SGrowingClient({
        baseUrl: 'https://api.test',
        fetch: mockFetch
      });

      await client.missions.step('8f7a61d1-6b45-4df3-a602-550be0e527d9', 'task-2');
      assert.equal(capturedUrl, 'https://api.test/api/v1/ai/missions/8f7a61d1-6b45-4df3-a602-550be0e527d9/approve/task-2');
    });

    it('should call missions.get', async () => {
      let capturedUrl = '';

      const mockFetch: typeof fetch = async (input) => {
        capturedUrl = input.toString();
        return new Response(JSON.stringify(mockPlan), { status: 200 });
      };

      const client = new SGrowingClient({
        baseUrl: 'https://api.test',
        fetch: mockFetch
      });

      const mission = await client.missions.get('8f7a61d1-6b45-4df3-a602-550be0e527d9');
      assert.equal(capturedUrl, 'https://api.test/api/v1/ai/missions/8f7a61d1-6b45-4df3-a602-550be0e527d9');
      assert.equal(mission.title, 'Q4 Black Friday Surge');
    });

    it('should call connectors.transformOrder', async () => {
      let capturedUrl = '';
      let capturedBody = '';

      const mockResult: TransformPayloadResult = {
        success: true,
        entityType: 'Order',
        transformedOrder: {
          externalOrderId: 'SP-1001',
          orderNumber: '#1001',
          totalAmount: 199.99,
          currency: 'USD',
          customerEmail: 'customer@example.com',
          customerPhone: '+1234567890',
          shippingCity: 'New York',
          shippingCountry: 'US',
          items: [
            {
              sku: 'SKU-001',
              title: 'Growth Hoodie',
              quantity: 1,
              unitPrice: 199.99,
              totalPrice: 199.99
            }
          ],
          orderDateUtc: new Date().toISOString()
        }
      };

      const mockFetch: typeof fetch = async (input, init) => {
        capturedUrl = input.toString();
        capturedBody = init?.body?.toString() ?? '';
        return new Response(JSON.stringify(mockResult), { status: 200 });
      };

      const client = new SGrowingClient({
        baseUrl: 'https://api.test',
        fetch: mockFetch
      });

      const result = await client.connectors.transformOrder('shopify', { id: 1001, total_price: '199.99' }, { 'customer.email': 'email' });
      assert.equal(capturedUrl, 'https://api.test/api/v1/connectors/marketplace/shopify/transform-order');
      assert.ok(capturedBody.includes('rawJsonPayload'));
      assert.equal(result.success, true);
      assert.equal(result.transformedOrder?.externalOrderId, 'SP-1001');
    });

    it('should call webhooks.dispatchTest and webhooks.verifySignature', async () => {
      let capturedUrl = '';
      let capturedBody = '';

      const mockFetch: typeof fetch = async (input, init) => {
        capturedUrl = input.toString();
        capturedBody = init?.body?.toString() ?? '';
        return new Response(JSON.stringify({ dispatchedCount: 1, logs: [] }), { status: 200 });
      };

      const client = new SGrowingClient({
        tenantId: 'tenant-abc-123',
        baseUrl: 'https://api.test',
        fetch: mockFetch
      });

      const res = await client.webhooks.dispatchTest('order.created', { orderId: '123' });
      assert.equal(capturedUrl, 'https://api.test/api/developer/webhooks/test-dispatch');
      assert.ok(capturedBody.includes('tenant-abc-123'));
      assert.equal(res.dispatchedCount, 1);

      // Verify signature via client helper
      const secret = 'whsec_secret';
      const body = '{"hello":"world"}';
      const sig = SGrowingWebhookVerifier.computeSignature(body, secret);
      assert.equal(client.webhooks.verifySignature(body, secret, sig), true);
    });

    it('should call telemetry.getRadar', async () => {
      let capturedUrl = '';

      const mockRadar: SreHealthRadarReport = {
        overallHealthScore: 99.8,
        overallStatus: 'HEALTHY',
        webhookErrorBudgetRemainingPercent: 99.95,
        p99WebhookDeliveryLatencyMs: 38.4,
        clickstreamEventsPerSec: 1250,
        clickstreamPartitionLag: 0,
        activeMissionsCount: 3,
        deadLetterQueueDepth: 0,
        activeAlerts: [],
        timestampUtc: new Date().toISOString()
      };

      const mockFetch: typeof fetch = async (input) => {
        capturedUrl = input.toString();
        return new Response(JSON.stringify(mockRadar), { status: 200 });
      };

      const client = new SGrowingClient({
        baseUrl: 'https://api.test',
        fetch: mockFetch
      });

      const radar = await client.telemetry.getRadar(0, 3);
      assert.equal(capturedUrl, 'https://api.test/api/v1/telemetry/sre-radar?dlqDepth=0&activeMissions=3');
      assert.equal(radar.overallStatus, 'HEALTHY');
      assert.equal(radar.overallHealthScore, 99.8);
    });

    it('should handle API errors and throw SGrowingApiError', async () => {
      const mockFetch: typeof fetch = async () => {
        return new Response(JSON.stringify({ error: 'Mission not found' }), {
          status: 404,
          statusText: 'Not Found'
        });
      };

      const client = new SGrowingClient({
        baseUrl: 'https://api.test',
        fetch: mockFetch
      });

      await assert.rejects(
        async () => {
          await client.missions.get('invalid-id');
        },
        (err: any) => {
          assert.ok(err instanceof SGrowingApiError);
          assert.equal(err.status, 404);
          assert.equal(err.message, 'Mission not found');
          return true;
        }
      );
    });
  });
});
