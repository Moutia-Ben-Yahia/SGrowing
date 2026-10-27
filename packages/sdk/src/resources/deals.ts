import { Deal, SGrowingClientConfig } from '../types.js';

export class DealsResource {
  constructor(private readonly config: SGrowingClientConfig) {}

  async list(): Promise<Deal[]> {
    const res = await fetch(`${this.config.baseUrl}/api/crm/deals?tenantId=${this.config.tenantId}`, {
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      }
    });
    if (!res.ok) throw new Error(`Failed to list deals: ${res.statusText}`);
    return res.json() as Promise<Deal[]>;
  }
}
