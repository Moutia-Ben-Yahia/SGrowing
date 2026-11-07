# @sgrowing/sdk

Official TypeScript / Node.js SDK for the SGrowing Enterprise Growth OS platform.

## Features

- **AI Missions**: Plan, step, approve, and retrieve autonomous multi-agent task DAGs.
- **Marketplace Connectors**: Normalize third-party commerce orders (Shopify, Salla, WooCommerce, Zid) into canonical formats.
- **Developer Webhooks**: Test dispatch and timing-safe constant-time HMAC-SHA256 signature verification with clock skew replay protection.
- **SRE Radar & Telemetry**: Query real-time platform vitals, SLO error budgets, and latency percentiles.

## Installation

```bash
npm install @sgrowing/sdk
```

## Quick Start

```typescript
import { SGrowingClient, SGrowingWebhookVerifier } from '@sgrowing/sdk';

// Initialize Client
const client = new SGrowingClient({
  baseUrl: 'https://api.sgrowing.io',
  apiKey: 'sg_live_...',
  tenantId: 'your-tenant-id'
});

// 1. Autonomous AI Missions
const plan = await client.missions.plan({
  title: 'Q4 Omnichannel Activation',
  strategicGoal: 'Maximize ROAS and customer reactivation',
  budgetLimit: 5000,
  targetGeo: 'US,UK'
});

// Step mission DAG
const updated = await client.missions.step(plan.missionId);

// 2. Connector Order Transformation
const result = await client.connectors.transformOrder('shopify', {
  id: 1001,
  total_price: '149.00'
});

// 3. Webhook Signature Verification
const isValid = SGrowingWebhookVerifier.verify(
  rawBody,
  process.env.WEBHOOK_SECRET!,
  req.headers['x-sg-signature']
);

// 4. SRE Health Radar
const radar = await client.telemetry.getRadar();
console.log(`System Status: ${radar.overallStatus}, Health Score: ${radar.overallHealthScore}`);
```

## License

MIT
