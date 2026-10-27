import { Contact, CreateContactParams, SGrowingClientConfig } from '../types.js';

export class ContactsResource {
  constructor(private readonly config: SGrowingClientConfig) {}

  async list(): Promise<Contact[]> {
    const res = await fetch(`${this.config.baseUrl}/api/crm/contacts?tenantId=${this.config.tenantId}`, {
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      }
    });
    if (!res.ok) throw new Error(`Failed to list contacts: ${res.statusText}`);
    return res.json() as Promise<Contact[]>;
  }

  async create(params: CreateContactParams): Promise<Contact> {
    const res = await fetch(`${this.config.baseUrl}/api/crm/contacts`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        tenantId: this.config.tenantId,
        ...params
      })
    });
    if (!res.ok) throw new Error(`Failed to create contact: ${res.statusText}`);
    return res.json() as Promise<Contact>;
  }
}
