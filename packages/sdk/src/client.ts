import { SGrowingClientConfig } from './types.js';
import { ContactsResource } from './resources/contacts.js';
import { DealsResource } from './resources/deals.js';
import { BillingResource } from './resources/billing.js';
import { WebhooksResource } from './resources/webhooks.js';
import { TelemetryResource } from './resources/telemetry.js';
import { AgentsResource } from './resources/agents.js';

export class SGrowingClient {
  public readonly contacts: ContactsResource;
  public readonly deals: DealsResource;
  public readonly billing: BillingResource;
  public readonly webhooks: WebhooksResource;
  public readonly telemetry: TelemetryResource;
  public readonly agents: AgentsResource;

  private readonly config: SGrowingClientConfig;

  constructor(config: SGrowingClientConfig) {
    if (!config.apiKey) {
      throw new Error('SGrowingClient requires an apiKey.');
    }
    if (!config.tenantId) {
      throw new Error('SGrowingClient requires a tenantId.');
    }

    this.config = {
      baseUrl: config.baseUrl ?? 'https://api.sgrowing.io',
      timeoutMs: config.timeoutMs ?? 30000,
      ...config
    };

    this.contacts = new ContactsResource(this.config);
    this.deals = new DealsResource(this.config);
    this.billing = new BillingResource(this.config);
    this.webhooks = new WebhooksResource(this.config);
    this.telemetry = new TelemetryResource(this.config);
    this.agents = new AgentsResource(this.config);
  }
}
