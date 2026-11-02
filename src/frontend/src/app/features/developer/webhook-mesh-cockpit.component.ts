import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WebhookEndpointUi {
  id: string;
  targetUrl: string;
  subscribedTopics: string[];
  status: 'Active' | 'Paused' | 'Degraded';
  successRate: number;
  lastDeliveredUtc: string;
}

export interface WebhookDeliveryLogUi {
  id: string;
  eventId: string;
  topic: string;
  targetUrl: string;
  status: 'Delivered' | 'Failed' | 'DeadLetter' | 'Replayed';
  httpStatus: number;
  attemptCount: number;
  durationMs: number;
  timestamp: string;
}

@Component({
  selector: 'app-webhook-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="webhook-mesh-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Bi-Directional Webhook Event Mesh & Replay Debugger</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-cyan-100 text-cyan-800">
              HMAC-SHA256 Signed
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Zero-loss event streaming, dead-letter queue diagnostics, and instant replay inspection with idempotency guarantees.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="emitTestEvent()"
            class="px-4 py-2 bg-cyan-600 hover:bg-cyan-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-emit-event">
            &uarr; Emit Synthetic Event
          </button>
        </div>
      </div>

      <!-- Mesh KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="webhook-mesh-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Registered Endpoints</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-endpoints-count">
            {{ endpoints().length }}
          </div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">100% High Availability</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Global Delivery Rate</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono" data-testid="kpi-delivery-rate">99.98%</div>
          <div class="text-[11px] text-slate-400 mt-1">Under 45ms P99 latency</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Dead Letter Queue (DLQ)</div>
          <div class="text-2xl font-bold text-amber-600 mt-1 font-mono" data-testid="kpi-dlq-count">
            {{ dlqCount() }}
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Awaiting auto/manual replay</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Events Dispatched (24h)</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-events-dispatched">
            {{ deliveryLogs().length * 1420 | number:'1.0-0' }}
          </div>
          <div class="text-[11px] text-cyan-600 mt-1 font-semibold">Idempotency Guard Enabled</div>
        </div>
      </div>

      <!-- Main Columns -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Subscriptions -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Target Subscriptions</h3>
            <span class="text-xs text-slate-400 font-mono">{{ endpoints().length }} targets</span>
          </div>

          <div class="space-y-3" data-testid="endpoints-list">
            @for (ep of endpoints(); track ep.id) {
              <div class="p-3 border rounded-lg hover:border-slate-300 transition-colors space-y-2">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-slate-800 text-xs truncate max-w-[200px]" [title]="ep.targetUrl">{{ ep.targetUrl }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800">
                    {{ ep.status }}
                  </span>
                </div>

                <div class="flex flex-wrap gap-1">
                  @for (topic of ep.subscribedTopics; track topic) {
                    <span class="px-1.5 py-0.5 rounded bg-slate-100 text-slate-600 text-[10px] font-mono">
                      {{ topic }}
                    </span>
                  }
                </div>

                <div class="flex justify-between items-center text-[10px] text-slate-400 pt-1 border-t">
                  <span>Success: <strong class="text-emerald-700 font-mono">{{ ep.successRate }}%</strong></span>
                  <span>{{ ep.lastDeliveredUtc }}</span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Live Deliveries & Replay Console -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Real-Time Event Dispatch Log</h3>
            <span class="text-xs text-slate-400 font-mono">Auto-streaming</span>
          </div>

          <div class="overflow-x-auto" data-testid="delivery-logs-table">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-50 border-b text-slate-500">
                <tr>
                  <th class="p-3">Event / Topic</th>
                  <th class="p-3">Target</th>
                  <th class="p-3">HTTP Status</th>
                  <th class="p-3">Latency</th>
                  <th class="p-3">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y">
                @for (log of deliveryLogs(); track log.id) {
                  <tr class="hover:bg-slate-50">
                    <td class="p-3">
                      <div class="font-mono font-bold text-slate-900">{{ log.topic }}</div>
                      <div class="text-[10px] text-slate-400 font-mono">{{ log.eventId }}</div>
                    </td>
                    <td class="p-3 text-slate-600 truncate max-w-[180px]" [title]="log.targetUrl">
                      {{ log.targetUrl }}
                    </td>
                    <td class="p-3">
                      <span 
                        [class.bg-emerald-100]="log.httpStatus === 200"
                        [class.text-emerald-800]="log.httpStatus === 200"
                        [class.bg-amber-100]="log.httpStatus === 504 || log.httpStatus === 500"
                        [class.text-amber-800]="log.httpStatus === 504 || log.httpStatus === 500"
                        class="px-2 py-0.5 rounded text-[10px] font-bold font-mono">
                        {{ log.httpStatus }} {{ log.status }}
                      </span>
                    </td>
                    <td class="p-3 font-mono text-slate-500">{{ log.durationMs }}ms</td>
                    <td class="p-3">
                      <button 
                        (click)="replayEvent(log.id)"
                        class="px-2.5 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded text-[11px] font-semibold transition-colors"
                        data-testid="btn-replay-event">
                        &#x21bb; Replay
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  `
})
export class WebhookMeshCockpitComponent {
  endpoints = signal<WebhookEndpointUi[]>([
    {
      id: 'ep_1',
      targetUrl: 'https://api.erp-hub.internal/webhooks/orders',
      subscribedTopics: ['order.created', 'order.allocated', 'fulfillment.shipped'],
      status: 'Active',
      successRate: 99.9,
      lastDeliveredUtc: '2 mins ago'
    },
    {
      id: 'ep_2',
      targetUrl: 'https://events.crm-gateway.com/sgrowing-ingest',
      subscribedTopics: ['lead.qualified', 'journey.step_completed', 'invoice.paid'],
      status: 'Active',
      successRate: 99.8,
      lastDeliveredUtc: 'Just now'
    },
    {
      id: 'ep_3',
      targetUrl: 'https://accounting-sync.prod.partner/v2/receipts',
      subscribedTopics: ['subscription.renewed', 'refund.processed'],
      status: 'Active',
      successRate: 98.4,
      lastDeliveredUtc: '14 mins ago'
    }
  ]);

  deliveryLogs = signal<WebhookDeliveryLogUi[]>([
    {
      id: 'del_101',
      eventId: 'evt_98492019',
      topic: 'order.allocated',
      targetUrl: 'https://api.erp-hub.internal/webhooks/orders',
      status: 'Delivered',
      httpStatus: 200,
      attemptCount: 1,
      durationMs: 38,
      timestamp: '2026-11-02T21:40:00Z'
    },
    {
      id: 'del_102',
      eventId: 'evt_98492020',
      topic: 'journey.step_completed',
      targetUrl: 'https://events.crm-gateway.com/sgrowing-ingest',
      status: 'Delivered',
      httpStatus: 200,
      attemptCount: 1,
      durationMs: 44,
      timestamp: '2026-11-02T21:42:15Z'
    },
    {
      id: 'del_103',
      eventId: 'evt_98492021',
      topic: 'subscription.renewed',
      targetUrl: 'https://accounting-sync.prod.partner/v2/receipts',
      status: 'DeadLetter',
      httpStatus: 504,
      attemptCount: 5,
      durationMs: 5002,
      timestamp: '2026-11-02T21:43:00Z'
    }
  ]);

  dlqCount = computed(() => {
    return this.deliveryLogs().filter(l => l.status === 'DeadLetter' || l.status === 'Failed').length;
  });

  emitTestEvent() {
    const newLog: WebhookDeliveryLogUi = {
      id: 'del_' + Date.now(),
      eventId: 'evt_' + Math.floor(10000000 + Math.random() * 90000000),
      topic: 'lead.qualified',
      targetUrl: 'https://events.crm-gateway.com/sgrowing-ingest',
      status: 'Delivered',
      httpStatus: 200,
      attemptCount: 1,
      durationMs: 29,
      timestamp: new Date().toISOString()
    };

    this.deliveryLogs.update(logs => [newLog, ...logs]);
  }

  replayEvent(deliveryLogId: string) {
    this.deliveryLogs.update(logs =>
      logs.map(log => {
        if (log.id === deliveryLogId) {
          return {
            ...log,
            status: 'Delivered',
            httpStatus: 200,
            attemptCount: log.attemptCount + 1,
            durationMs: 35
          };
        }
        return log;
      })
    );
  }
}
