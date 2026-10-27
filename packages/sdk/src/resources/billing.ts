import { PlanQuotaStatus, PlanTier, SGrowingClientConfig, UsageMetricType } from '../types.js';

export class BillingResource {
  constructor(private readonly config: SGrowingClientConfig) {}

  async getQuotaStatus(): Promise<PlanQuotaStatus> {
    const res = await fetch(`${this.config.baseUrl}/api/billing/quota/status?tenantId=${this.config.tenantId}`, {
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      }
    });
    if (!res.ok) throw new Error(`Failed to get quota status: ${res.statusText}`);
    return res.json() as Promise<PlanQuotaStatus>;
  }

  async checkQuota(metric: UsageMetricType, requestedQuantity: number = 1): Promise<{ allowed: boolean; isHardBlocked: boolean; message: string }> {
    const res = await fetch(`${this.config.baseUrl}/api/billing/quota/enforce`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        tenantId: this.config.tenantId,
        metric,
        requestedQuantity
      })
    });
    return res.json();
  }

  async upgradePlan(targetTier: PlanTier): Promise<unknown> {
    const res = await fetch(`${this.config.baseUrl}/api/billing/upgrade`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        tenantId: this.config.tenantId,
        targetTier
      })
    });
    if (!res.ok) throw new Error(`Failed to upgrade plan: ${res.statusText}`);
    return res.json();
  }
}
