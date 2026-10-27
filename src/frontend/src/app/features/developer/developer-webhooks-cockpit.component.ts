import { Component, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface WebhookSubscriptionUi {
  id: string;
  targetUrl: string;
  events: string[];
  secretMasked: string;
  isActive: boolean;
  createdAtUtc: string;
  successfulDeliveries: number;
  failedDeliveries: number;
}

export interface WebhookLogUi {
  id: string;
  subscriptionId: string;
  eventType: string;
  targetUrl: string;
  httpStatusCode: number;
  attemptNumber: number;
  maxRetries: number;
  isDeadLetter: boolean;
  deadLetterReason?: string;
  signatureHeader: string;
  timestampHeader: string;
  durationMs: number;
  dispatchedAtUtc: string;
}

@Component({
  selector: 'app-developer-webhooks-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/70 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/developer" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Developer Studio
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              ⚡ Outward Webhook Delivery Engine & Public SDK
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              HMAC-SHA256 • Exponential Backoff • DLQ
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="triggerTestDispatch()"
              class="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md">
              <span>🚀</span> Test Dispatch
            </button>
            <button
              (click)="showCreateModal.set(true)"
              class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md">
              <span>+</span> Add Endpoint
            </button>
          </div>
        </header>

        <div class="p-6 max-w-7xl mx-auto w-full space-y-6 flex-1">
          <!-- Overview KPI row -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/50">
              <span class="text-xs text-slate-400 block mb-1">Active Endpoints</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-white">{{ activeEndpointsCount() }}</span>
                <span class="text-xs text-emerald-400 font-mono">100% Healthy</span>
              </div>
            </div>

            <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/50">
              <span class="text-xs text-slate-400 block mb-1">24h Deliveries</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-indigo-400">14,280</span>
                <span class="text-xs text-slate-400 font-mono">99.8% Success</span>
              </div>
            </div>

            <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/50">
              <span class="text-xs text-slate-400 block mb-1">Dead-Letter Queue (DLQ)</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold" [ngClass]="dlqLogs().length > 0 ? 'text-amber-400' : 'text-slate-400'">
                  {{ dlqLogs().length }}
                </span>
                <span class="text-xs text-amber-400 font-mono">Requires Attention</span>
              </div>
            </div>

            <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/50">
              <span class="text-xs text-slate-400 block mb-1">Avg Dispatch Latency</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-emerald-400">84ms</span>
                <span class="text-xs text-slate-400 font-mono">p95: 142ms</span>
              </div>
            </div>
          </div>

          <!-- Registered Endpoints Section -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono">
                  Registered Outward Webhook Endpoints
                </h2>
                <p class="text-xs text-slate-400">
                  Target endpoints receiving real-time signed HTTP POST webhooks upon domain events.
                </p>
              </div>
            </div>

            <div class="divide-y divide-slate-800/80 border border-slate-800 rounded-xl overflow-hidden bg-slate-950/40">
              @for (sub of subscriptions(); track sub.id) {
                <div class="p-4 flex flex-col md:flex-row md:items-center justify-between gap-4 hover:bg-slate-900/40 transition">
                  <div class="space-y-1">
                    <div class="flex items-center gap-2">
                      <span class="h-2 w-2 rounded-full" [ngClass]="sub.isActive ? 'bg-emerald-400 animate-pulse' : 'bg-slate-600'"></span>
                      <span class="font-mono text-xs font-bold text-white">{{ sub.targetUrl }}</span>
                      <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-400 border border-slate-750">
                        Secret: {{ sub.secretMasked }}
                      </span>
                    </div>
                    <div class="flex flex-wrap gap-1.5 pt-1">
                      @for (ev of sub.events; track ev) {
                        <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-indigo-950/60 text-indigo-300 border border-indigo-500/30">
                          {{ ev }}
                        </span>
                      }
                    </div>
                  </div>

                  <div class="flex items-center gap-4 shrink-0">
                    <div class="text-right text-[11px] font-mono text-slate-400">
                      <div>Success: <strong class="text-emerald-400">{{ sub.successfulDeliveries }}</strong></div>
                      <div>Failed: <strong class="text-rose-400">{{ sub.failedDeliveries }}</strong></div>
                    </div>

                    <button
                      (click)="toggleSubscription(sub.id)"
                      [class]="sub.isActive ? 'bg-slate-800 text-slate-300 hover:bg-slate-750' : 'bg-emerald-600/30 text-emerald-300 hover:bg-emerald-600/40 border border-emerald-500/40'"
                      class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
                      {{ sub.isActive ? 'Disable' : 'Enable' }}
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- Dead-Letter Queue (DLQ) Inspector -->
          <div class="bg-slate-900/70 border border-amber-500/30 rounded-2xl p-6 shadow-xl space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <div class="flex items-center gap-2">
                  <span class="text-amber-400 text-base">⚠️</span>
                  <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono">
                    Dead-Letter Queue (DLQ) Management
                  </h2>
                  <span class="px-2 py-0.5 text-[10px] font-mono rounded bg-amber-500/20 text-amber-300 border border-amber-500/40">
                    {{ dlqLogs().length }} Dead Letters
                  </span>
                </div>
                <p class="text-xs text-slate-400 mt-0.5">
                  Webhooks that exhausted 5 exponential backoff retry cycles (1m, 5m, 15m, 1h, 6h).
                </p>
              </div>

              @if (dlqLogs().length > 0) {
                <button
                  (click)="retryAllDlq()"
                  class="px-4 py-1.5 bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold rounded-lg text-xs transition shadow-md shadow-amber-500/20 flex items-center gap-1.5">
                  <span>🔄</span> Replay All Dead Letters
                </button>
              }
            </div>

            @if (dlqLogs().length === 0) {
              <div class="p-6 text-center bg-slate-950/40 rounded-xl border border-slate-800 text-xs text-slate-400">
                🎉 No dead-letter events. All outward webhook dispatches are healthy.
              </div>
            } @else {
              <div class="border border-slate-800 rounded-xl overflow-x-auto bg-slate-950/40">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-950 text-slate-400 font-mono text-[11px] border-b border-slate-800">
                    <tr>
                      <th class="p-3">Event Type</th>
                      <th class="p-3">Target URL</th>
                      <th class="p-3">Attempts</th>
                      <th class="p-3">Failure Reason</th>
                      <th class="p-3">Last Attempt</th>
                      <th class="p-3 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-mono text-[11px]">
                    @for (item of dlqLogs(); track item.id) {
                      <tr class="hover:bg-slate-800/30">
                        <td class="p-3 font-bold text-indigo-300">{{ item.eventType }}</td>
                        <td class="p-3 text-slate-300 max-w-xs truncate">{{ item.targetUrl }}</td>
                        <td class="p-3 text-rose-400">{{ item.attemptNumber }}/{{ item.maxRetries }}</td>
                        <td class="p-3 text-slate-400 italic max-w-xs truncate">{{ item.deadLetterReason }}</td>
                        <td class="p-3 text-slate-400">{{ item.dispatchedAtUtc }}</td>
                        <td class="p-3 text-right">
                          <button
                            (click)="retryDlqItem(item.id)"
                            class="px-3 py-1 bg-slate-800 hover:bg-slate-700 text-amber-300 rounded border border-slate-700 text-xs transition">
                            Retry
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </div>

          <!-- Live Delivery Logs Stream -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono">
                  Live Dispatch Stream & Audit Trail
                </h2>
                <p class="text-xs text-slate-400">
                  Real-time HTTP delivery logs, latency benchmarks, and cryptographic signatures.
                </p>
              </div>
              <span class="text-xs font-mono text-emerald-400 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-emerald-400 animate-ping"></span> Live Stream
              </span>
            </div>

            <div class="border border-slate-800 rounded-xl overflow-x-auto bg-slate-950/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-950 text-slate-400 font-mono text-[11px] border-b border-slate-800">
                  <tr>
                    <th class="p-3">Status</th>
                    <th class="p-3">Event</th>
                    <th class="p-3">Target URL</th>
                    <th class="p-3">Attempt</th>
                    <th class="p-3">Latency</th>
                    <th class="p-3">HMAC-SHA256 Signature</th>
                    <th class="p-3">Dispatched At</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono text-[11px]">
                  @for (log of logs(); track log.id) {
                    <tr class="hover:bg-slate-800/30">
                      <td class="p-3">
                        <span class="px-2 py-0.5 rounded font-bold"
                          [ngClass]="log.httpStatusCode >= 200 && log.httpStatusCode < 300 ? 'bg-emerald-950 text-emerald-400 border border-emerald-500/30' : 'bg-rose-950 text-rose-400 border border-rose-500/30'">
                          HTTP {{ log.httpStatusCode }}
                        </span>
                      </td>
                      <td class="p-3 font-bold text-indigo-300">{{ log.eventType }}</td>
                      <td class="p-3 text-slate-300 max-w-xs truncate">{{ log.targetUrl }}</td>
                      <td class="p-3 text-slate-400">#{{ log.attemptNumber }}</td>
                      <td class="p-3 text-slate-300">{{ log.durationMs }}ms</td>
                      <td class="p-3 text-slate-500 text-[10px] max-w-xs truncate font-mono" title="{{ log.signatureHeader }}">
                        {{ log.signatureHeader }}
                      </td>
                      <td class="p-3 text-slate-400">{{ log.dispatchedAtUtc }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <!-- Official TypeScript SDK Quick-Start Card -->
          <div class="bg-gradient-to-br from-indigo-950/40 to-slate-900 border border-indigo-500/30 rounded-2xl p-6 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span class="text-xl">📦</span>
                <h3 class="text-sm font-bold text-white uppercase tracking-wider font-mono">
                  Official TypeScript SDK Integration: &#64;sgrowing/sdk
                </h3>
              </div>
              <span class="text-xs font-mono text-indigo-400 bg-indigo-950 px-2.5 py-0.5 rounded border border-indigo-700">
                npm i &#64;sgrowing/sdk
              </span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs font-mono">
              <!-- Snippet 1: Client instantiation -->
              <div class="p-4 rounded-xl bg-slate-950/90 border border-slate-800 space-y-2">
                <span class="text-slate-400 block font-sans text-[11px]">1. Client Initialization & Resource Query:</span>
                <pre class="text-indigo-300 overflow-x-auto whitespace-pre">import &#123; SGrowingClient &#125; from '&#64;sgrowing/sdk';

const client = new SGrowingClient(&#123;
  apiKey: 'sg_live_7a8f9c...',
  tenantId: '00000000-0000-...'
&#125;);

// Query leads or deals
const leads = await client.contacts.list(&#123; limit: 20 &#125;);</pre>
              </div>

              <!-- Snippet 2: Signature verification -->
              <div class="p-4 rounded-xl bg-slate-950/90 border border-slate-800 space-y-2">
                <span class="text-slate-400 block font-sans text-[11px]">2. Webhook HMAC-SHA256 Verification:</span>
                <pre class="text-emerald-300 overflow-x-auto whitespace-pre">import &#123; verifyWebhookSignature &#125; from '&#64;sgrowing/sdk';

const isValid = verifyWebhookSignature(
  rawBody,
  req.headers['x-sgrowing-signature'],
  req.headers['x-sgrowing-timestamp'],
  process.env.WEBHOOK_SECRET
);

if (!isValid) throw new Error('Untrusted signature!');</pre>
              </div>
            </div>
          </div>
        </div>
      </main>

      <!-- Add Endpoint Modal -->
      @if (showCreateModal()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 space-y-5 shadow-2xl">
            <div class="flex items-center justify-between">
              <h3 class="text-base font-bold text-white">Register Webhook Endpoint</h3>
              <button (click)="showCreateModal.set(false)" class="text-slate-400 hover:text-white">✕</button>
            </div>

            <div class="space-y-4 text-xs">
              <div>
                <label class="block text-slate-300 mb-1">Target URL</label>
                <input
                  type="url"
                  [(ngModel)]="newTargetUrl"
                  placeholder="https://api.yourcompany.com/webhooks/sgrowing"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono text-xs" />
              </div>

              <div>
                <label class="block text-slate-300 mb-2">Subscribe to Events</label>
                <div class="grid grid-cols-2 gap-2 font-mono text-[11px]">
                  @for (ev of availableEvents; track ev) {
                    <label class="flex items-center gap-2 p-2 bg-slate-950 rounded-lg border border-slate-800 cursor-pointer">
                      <input
                        type="checkbox"
                        [checked]="isEventSelected(ev)"
                        (change)="toggleEventSelection(ev)"
                        class="accent-indigo-600 rounded" />
                      <span class="text-slate-300">{{ ev }}</span>
                    </label>
                  }
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-3 pt-3 border-t border-slate-800">
              <button (click)="showCreateModal.set(false)" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-xs">
                Cancel
              </button>
              <button
                (click)="addEndpoint()"
                class="px-5 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold transition shadow-lg shadow-indigo-600/20">
                Save Webhook
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class DeveloperWebhooksCockpitComponent implements OnInit {
  showCreateModal = signal<boolean>(false);
  newTargetUrl = 'https://api.merchant-platform.io/events/sgrowing';
  selectedEvents = signal<string[]>(['contact.created', 'deal.won', 'contract.signed']);

  availableEvents = [
    'contact.created',
    'deal.won',
    'contract.signed',
    'order.delivered',
    'fraud.flagged'
  ];

  subscriptions = signal<WebhookSubscriptionUi[]>([
    {
      id: 'sub-001',
      targetUrl: 'https://api.merchant-platform.io/events/sgrowing',
      events: ['contact.created', 'deal.won', 'contract.signed'],
      secretMasked: 'whsec_••••••••••••9f4a',
      isActive: true,
      createdAtUtc: '2026-10-20 10:00:00',
      successfulDeliveries: 12450,
      failedDeliveries: 3
    },
    {
      id: 'sub-002',
      targetUrl: 'https://logistics.maghrebhub.tn/webhooks/orders',
      events: ['order.delivered', 'fraud.flagged'],
      secretMasked: 'whsec_••••••••••••83c1',
      isActive: true,
      createdAtUtc: '2026-10-22 14:30:00',
      successfulDeliveries: 1830,
      failedDeliveries: 1
    }
  ]);

  logs = signal<WebhookLogUi[]>([
    {
      id: 'log-101',
      subscriptionId: 'sub-001',
      eventType: 'deal.won',
      targetUrl: 'https://api.merchant-platform.io/events/sgrowing',
      httpStatusCode: 200,
      attemptNumber: 1,
      maxRetries: 5,
      isDeadLetter: false,
      signatureHeader: 't=1793112800,v1=9b3a4f6e1c8d0a7b5e4f2a1b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d',
      timestampHeader: '1793112800',
      durationMs: 78,
      dispatchedAtUtc: 'Just now'
    },
    {
      id: 'log-102',
      subscriptionId: 'sub-001',
      eventType: 'contract.signed',
      targetUrl: 'https://api.merchant-platform.io/events/sgrowing',
      httpStatusCode: 200,
      attemptNumber: 1,
      maxRetries: 5,
      isDeadLetter: false,
      signatureHeader: 't=1793112750,v1=7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a',
      timestampHeader: '1793112750',
      durationMs: 92,
      dispatchedAtUtc: '2 mins ago'
    },
    {
      id: 'log-103',
      subscriptionId: 'sub-002',
      eventType: 'fraud.flagged',
      targetUrl: 'https://logistics.maghrebhub.tn/webhooks/orders',
      httpStatusCode: 200,
      attemptNumber: 1,
      maxRetries: 5,
      isDeadLetter: false,
      signatureHeader: 't=1793112600,v1=4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b',
      timestampHeader: '1793112600',
      durationMs: 110,
      dispatchedAtUtc: '5 mins ago'
    }
  ]);

  dlqLogs = signal<WebhookLogUi[]>([
    {
      id: 'dlq-201',
      subscriptionId: 'sub-001',
      eventType: 'order.delivered',
      targetUrl: 'https://broken-endpoint.external-erp.com/hook',
      httpStatusCode: 504,
      attemptNumber: 5,
      maxRetries: 5,
      isDeadLetter: true,
      deadLetterReason: 'Gateway Timeout (504) after 5 retries (1m, 5m, 15m, 1h, 6h)',
      signatureHeader: 't=1793100000,v1=1234abcd5678ef01...',
      timestampHeader: '1793100000',
      durationMs: 5002,
      dispatchedAtUtc: '6 hours ago'
    }
  ]);

  activeEndpointsCount = computed(() => this.subscriptions().filter(s => s.isActive).length);

  ngOnInit() {}

  isEventSelected(ev: string): boolean {
    return this.selectedEvents().includes(ev);
  }

  toggleEventSelection(ev: string) {
    this.selectedEvents.update(list =>
      list.includes(ev) ? list.filter(e => e !== ev) : [...list, ev]
    );
  }

  toggleSubscription(id: string) {
    this.subscriptions.update(list =>
      list.map(s => s.id === id ? { ...s, isActive: !s.isActive } : s)
    );
  }

  triggerTestDispatch() {
    const testLog: WebhookLogUi = {
      id: `log-${Date.now()}`,
      subscriptionId: 'sub-001',
      eventType: 'contact.created',
      targetUrl: 'https://api.merchant-platform.io/events/sgrowing',
      httpStatusCode: 200,
      attemptNumber: 1,
      maxRetries: 5,
      isDeadLetter: false,
      signatureHeader: `t=${Math.floor(Date.now() / 1000)},v1=a1b2c3d4e5f67890abcdef1234567890abcdef1234567890abcdef1234567890`,
      timestampHeader: `${Math.floor(Date.now() / 1000)}`,
      durationMs: 64,
      dispatchedAtUtc: 'Just now'
    };

    this.logs.update(list => [testLog, ...list]);
  }

  retryDlqItem(id: string) {
    this.dlqLogs.update(list => list.filter(item => item.id !== id));
    this.triggerTestDispatch();
  }

  retryAllDlq() {
    this.dlqLogs.set([]);
    this.triggerTestDispatch();
  }

  addEndpoint() {
    if (!this.newTargetUrl) return;

    const newSub: WebhookSubscriptionUi = {
      id: `sub-${Date.now()}`,
      targetUrl: this.newTargetUrl,
      events: [...this.selectedEvents()],
      secretMasked: 'whsec_••••••••••••' + Math.random().toString(16).substring(2, 6),
      isActive: true,
      createdAtUtc: new Date().toISOString(),
      successfulDeliveries: 0,
      failedDeliveries: 0
    };

    this.subscriptions.update(list => [...list, newSub]);
    this.showCreateModal.set(false);
  }
}
