import { IngestTelemetryParams, SGrowingClientConfig, TelemetryEvent } from '../types.js';

export class TelemetryResource {
  constructor(private readonly config: SGrowingClientConfig) {}

  async track(params: IngestTelemetryParams): Promise<TelemetryEvent> {
    const res = await fetch(`${this.config.baseUrl}/api/telemetry/events`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        tenantId: this.config.tenantId,
        sessionId: params.sessionId,
        visitorId: params.visitorId,
        eventName: params.eventName,
        url: params.url,
        referrer: params.referrer ?? '',
        deviceType: params.deviceType ?? 'Desktop',
        browser: params.browser ?? 'Chrome',
        countryCode: params.countryCode ?? 'TN',
        city: params.city ?? 'Tunis',
        propertiesJson: JSON.stringify(params.properties ?? {})
      })
    });
    if (!res.ok) throw new Error(`Failed to track telemetry event: ${res.statusText}`);
    return res.json() as Promise<TelemetryEvent>;
  }
}
