import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { TenantApiKeyDto, WebhookSubscriptionRecordDto, WebhookDeliveryAttemptLogDto } from '../../core/models/developer-webhooks.models';

@Component({
  selector: 'app-developer-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100 overflow-y-auto">
      <!-- Header -->
      <div class="p-6 border-b border-slate-800 bg-slate-900/40 backdrop-blur flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <span class="text-2xl">🔌</span>
            <h1 class="text-xl font-bold tracking-tight text-white">Developer API & Outbound Webhooks Mesh</h1>
            <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
              n8n / Twenty Architecture
            </span>
          </div>
          <p class="text-xs text-slate-400 mt-1">
            Generate scoped API tokens, manage outbound HMAC-SHA256 event subscriptions, and inspect delivery retry pipelines.
          </p>
        </div>

        <!-- Navigation Tabs -->
        <div class="flex items-center gap-1 bg-slate-900 p-1 rounded-xl border border-slate-800 text-xs">
          <button
            (click)="activeTab.set('keys')"
            [class.bg-cyan-600]="activeTab() === 'keys'"
            [class.text-white]="activeTab() === 'keys'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            🔑 API Keys
          </button>
          <button
            (click)="activeTab.set('webhooks')"
            [class.bg-cyan-600]="activeTab() === 'webhooks'"
            [class.text-white]="activeTab() === 'webhooks'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            🌐 Webhook Subscriptions
          </button>
          <button
            (click)="activeTab.set('logs')"
            [class.bg-cyan-600]="activeTab() === 'logs'"
            [class.text-white]="activeTab() === 'logs'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            📡 Delivery Logs ({{ logs().length }})
          </button>
        </div>
      </div>

      <!-- Main Content -->
      <div class="p-6 space-y-6 flex-1">
        <!-- 1. API Keys Tab -->
        @if (activeTab() === 'keys') {
          <div class="space-y-6">
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <h3 class="text-sm font-bold text-white">Create Developer API Key</h3>
                <p class="text-xs text-slate-400 mt-1">Authenticate server-to-server integrations, Zapier apps, and warehouse scanners.</p>
              </div>
              <div class="flex items-center gap-3">
                <input [(ngModel)]="newKeyName" placeholder="Key Name (e.g. ERP Integration)" class="px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-xs text-slate-200" />
                <button
                  (click)="generateApiKey()"
                  class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-xs font-bold transition">
                  Create Key
                </button>
              </div>
            </div>

            @if (plainKeySecret()) {
              <div class="p-4 rounded-xl bg-cyan-500/10 border border-cyan-500/20 space-y-2">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-bold text-cyan-400">⚠️ Copy your API Key now. You won't be able to see it again!</span>
                  <span class="text-[10px] text-slate-400 font-mono">Store securely in your secrets manager</span>
                </div>
                <input [value]="plainKeySecret()" readonly class="w-full px-3 py-2 bg-slate-950 border border-cyan-500/40 rounded-lg text-xs font-mono text-cyan-300 font-bold" />
              </div>
            }

            <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-900/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-900/80 text-slate-400 uppercase font-mono border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3">Key Name</th>
                    <th class="px-4 py-3">Token Mask</th>
                    <th class="px-4 py-3">Scopes</th>
                    <th class="px-4 py-3">Rate Limit</th>
                    <th class="px-4 py-3">Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (k of apiKeys(); track k.id) {
                    <tr class="hover:bg-slate-900/40">
                      <td class="px-4 py-3 text-white font-sans font-bold">{{ k.name }}</td>
                      <td class="px-4 py-3 text-cyan-400">{{ k.keyMasked }}</td>
                      <td class="px-4 py-3 text-slate-400">{{ k.allowedScopesJson }}</td>
                      <td class="px-4 py-3 text-slate-300">{{ k.rateLimitPerMinute }} req/min</td>
                      <td class="px-4 py-3">
                        @if (!k.isRevoked) {
                          <button (click)="revokeKey(k.id)" class="text-rose-400 hover:text-rose-300 underline text-xs">Revoke</button>
                        } @else {
                          <span class="text-slate-500 text-xs">Revoked</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- 2. Webhooks Tab -->
        @if (activeTab() === 'webhooks') {
          <div class="space-y-6">
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <h3 class="text-sm font-bold text-white">Webhook Subscriptions</h3>
                <p class="text-xs text-slate-400 mt-1">Receive event payloads securely via HMAC-SHA256 signed POST requests.</p>
              </div>
              <div class="flex items-center gap-3">
                <input [(ngModel)]="newWebhookUrl" placeholder="https://api.yourdomain.com/webhook" class="px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-xs text-slate-200 min-w-[280px]" />
                <button
                  (click)="registerWebhook()"
                  class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-xs font-bold transition">
                  Add Webhook
                </button>
              </div>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              @for (sub of subscriptions(); track sub.id) {
                <div class="p-4 rounded-xl bg-slate-900/40 border border-slate-800 space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-bold text-white truncate max-w-[70%]">{{ sub.targetUrl }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono"
                      [class.bg-emerald-500/20]="sub.isActive"
                      [class.text-emerald-300]="sub.isActive"
                      [class.bg-slate-800]="!sub.isActive"
                      [class.text-slate-500]="!sub.isActive">
                      {{ sub.isActive ? 'Active' : 'Disabled' }}
                    </span>
                  </div>
                  <div class="p-2.5 bg-slate-950 rounded border border-slate-800 font-mono text-[11px] text-slate-400">
                    <span class="text-slate-500 block mb-0.5">Signing Secret:</span>
                    <span class="text-cyan-400">{{ sub.secretKey }}</span>
                  </div>
                  <div class="flex items-center justify-between text-xs">
                    <span class="text-slate-400 font-mono">{{ sub.subscribedEventsJson }}</span>
                    <button (click)="testDispatch()" class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-cyan-300 rounded text-xs border border-slate-700 transition">
                      ⚡ Test Dispatch
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- 3. Logs Tab -->
        @if (activeTab() === 'logs') {
          <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-900/40">
            <div class="p-4 border-b border-slate-800 flex justify-between items-center">
              <span class="text-xs font-bold text-slate-200">Recent Webhook Deliveries</span>
              <button (click)="testDispatch()" class="px-3 py-1 bg-cyan-600 text-white rounded text-xs font-bold">
                ⚡ Push Test Event
              </button>
            </div>
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-900/80 text-slate-400 uppercase font-mono border-b border-slate-800">
                <tr>
                  <th class="px-4 py-3">Event Type</th>
                  <th class="px-4 py-3">Response</th>
                  <th class="px-4 py-3">Latency</th>
                  <th class="px-4 py-3">Status</th>
                  <th class="px-4 py-3">Timestamp</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60 font-mono">
                @for (log of logs(); track log.id) {
                  <tr class="hover:bg-slate-900/40">
                    <td class="px-4 py-3 text-cyan-400 font-bold">{{ log.eventType }}</td>
                    <td class="px-4 py-3">
                      <span class="px-2 py-0.5 rounded text-[10px]"
                        [class.bg-emerald-500/20]="log.responseStatusCode >= 200 && log.responseStatusCode < 300"
                        [class.text-emerald-300]="log.responseStatusCode >= 200 && log.responseStatusCode < 300"
                        [class.bg-rose-500/20]="log.responseStatusCode >= 400"
                        [class.text-rose-300]="log.responseStatusCode >= 400">
                        {{ log.responseStatusCode }} OK
                      </span>
                    </td>
                    <td class="px-4 py-3 text-slate-400">{{ log.executionDurationMs }}ms</td>
                    <td class="px-4 py-3 text-emerald-400">{{ log.isSuccess ? 'Delivered' : 'Failed' }}</td>
                    <td class="px-4 py-3 text-slate-400">{{ log.attemptedAtUtc | date:'mediumTime' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `
})
export class DeveloperStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public tenantId = this.auth.currentTenant()?.id || 'a0000000-0000-0000-0000-000000000001';
  public activeTab = signal<'keys' | 'webhooks' | 'logs'>('keys');

  public apiKeys = signal<TenantApiKeyDto[]>([]);
  public subscriptions = signal<WebhookSubscriptionRecordDto[]>([]);
  public logs = signal<WebhookDeliveryAttemptLogDto[]>([]);

  public newKeyName = 'ERP Automation Engine';
  public plainKeySecret = signal<string | null>(null);
  public newWebhookUrl = 'https://api.merchant-portal.tn/events/incoming';

  ngOnInit(): void {
    this.loadAll();
  }

  public loadAll(): void {
    this.api.getApiKeys(this.tenantId).subscribe(keys => {
      if (keys.length === 0) {
        this.generateApiKey();
      } else {
        this.apiKeys.set(keys);
      }
    });

    this.api.getWebhookSubscriptions(this.tenantId).subscribe(subs => {
      if (subs.length === 0) {
        this.registerWebhook();
      } else {
        this.subscriptions.set(subs);
      }
    });

    this.api.getWebhookLogs(this.tenantId).subscribe(logs => this.logs.set(logs));
  }

  public generateApiKey(): void {
    this.api.createApiKey({
      tenantId: this.tenantId,
      name: this.newKeyName,
      scopes: ['orders:read', 'orders:write', 'contacts:read'],
      isLive: true,
      rateLimitPerMinute: 240
    }).subscribe(res => {
      this.plainKeySecret.set(res.plainKey);
      this.apiKeys.update(prev => [res, ...prev]);
    });
  }

  public revokeKey(id: string): void {
    this.api.revokeApiKey(this.tenantId, id).subscribe(() => {
      this.apiKeys.update(prev => prev.map(k => k.id === id ? { ...k, isRevoked: true } : k));
    });
  }

  public registerWebhook(): void {
    this.api.createWebhookSubscription({
      tenantId: this.tenantId,
      targetUrl: this.newWebhookUrl,
      events: ['order.delivered', 'form.submitted', 'booking.confirmed']
    }).subscribe(sub => {
      this.subscriptions.update(prev => [sub, ...prev]);
    });
  }

  public testDispatch(): void {
    this.api.testDispatchWebhook({
      tenantId: this.tenantId,
      eventType: 'order.delivered',
      payload: {
        orderId: 'a123-b456-c789',
        orderNumber: 'ORD-TEST-992',
        deliveredAtUtc: new Date().toISOString()
      }
    }).subscribe(res => {
      if (res.logs) {
        this.logs.update(prev => [...res.logs, ...prev]);
        this.activeTab.set('logs');
      }
    });
  }
}
