import { SGrowingClientConfig, WebhookDeliveryLog, WebhookSubscription } from '../types.js';

export class WebhooksResource {
  constructor(private readonly config: SGrowingClientConfig) {}

  async listSubscriptions(): Promise<WebhookSubscription[]> {
    const res = await fetch(`${this.config.baseUrl}/api/developer/webhooks/subscriptions?tenantId=${this.config.tenantId}`, {
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      }
    });
    if (!res.ok) throw new Error(`Failed to list subscriptions: ${res.statusText}`);
    return res.json() as Promise<WebhookSubscription[]>;
  }

  async createSubscription(targetUrl: string, events: string[]): Promise<WebhookSubscription> {
    const res = await fetch(`${this.config.baseUrl}/api/developer/webhooks/subscriptions`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        tenantId: this.config.tenantId,
        targetUrl,
        events
      })
    });
    if (!res.ok) throw new Error(`Failed to create webhook subscription: ${res.statusText}`);
    return res.json() as Promise<WebhookSubscription>;
  }

  async listDeadLetters(limit: number = 50): Promise<WebhookDeliveryLog[]> {
    const res = await fetch(`${this.config.baseUrl}/api/developer/webhooks/dlq?tenantId=${this.config.tenantId}&limit=${limit}`, {
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      }
    });
    if (!res.ok) throw new Error(`Failed to list DLQ: ${res.statusText}`);
    return res.json() as Promise<WebhookDeliveryLog[]>;
  }

  async retryDeadLetter(deliveryLogId: string): Promise<WebhookDeliveryLog> {
    const res = await fetch(`${this.config.baseUrl}/api/developer/webhooks/dlq/${deliveryLogId}/retry?tenantId=${this.config.tenantId}`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      }
    });
    if (!res.ok) throw new Error(`Failed to retry delivery: ${res.statusText}`);
    return res.json() as Promise<WebhookDeliveryLog>;
  }
}
