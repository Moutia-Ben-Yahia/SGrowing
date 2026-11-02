import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type DeveloperTier = 'Starter' | 'Growth' | 'Enterprise' | 'Partner';

export interface TierQuota {
  tier: DeveloperTier;
  rpm: number;
  burst: number;
  refillPerSec: number;
}

export interface SimulationResult {
  statusCode: number;
  latencyMs: number;
  traceId: string;
  responsePayload: string;
  timestamp: string;
}

@Component({
  selector: 'app-developer-sandbox-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-700 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-indigo-600/20 text-indigo-400 rounded-lg text-xl">🛠️</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Public Developer Sandbox & Rate Limiting Gateway</h1>
              <p class="text-sm text-slate-400">Token-bucket rate limits, mock conversion simulator, and role-partitioned OpenAPI 3.0 export</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold"
                [ngClass]="isSandboxMode() ? 'bg-amber-500/20 text-amber-300 border border-amber-500/30' : 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'">
            {{ isSandboxMode() ? '● Sandbox Isolated' : '● Live Production Gateway' }}
          </span>
          <button (click)="toggleSandboxMode()"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-600 transition">
            Switch to {{ isSandboxMode() ? 'Live Mode' : 'Sandbox' }}
          </button>
        </div>
      </div>

      <!-- Grid layout -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Left: Token-Bucket Rate Limiter (5 cols) -->
        <div class="lg:col-span-5 bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col justify-between">
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <h2 class="text-base font-semibold text-white flex items-center gap-2">
                <span>⏱️</span> Token Bucket Rate Limiter
              </h2>
              <span class="text-xs text-slate-400">{{ activeTier() }} Tier</span>
            </div>

            <!-- Tier Selector -->
            <div class="grid grid-cols-4 gap-2">
              @for (tier of tiers; track tier) {
                <button (click)="selectTier(tier)"
                        class="px-2 py-1.5 rounded-lg text-xs font-medium transition text-center"
                        [ngClass]="activeTier() === tier ? 'bg-indigo-600 text-white font-semibold shadow' : 'bg-slate-800 text-slate-400 hover:text-slate-200'">
                  {{ tier }}
                </button>
              }
            </div>

            <!-- Bucket Tank Visualizer -->
            <div class="p-4 bg-slate-950/80 rounded-xl border border-slate-800 space-y-3">
              <div class="flex justify-between text-xs text-slate-300 font-medium">
                <span>Remaining Tokens:</span>
                <span class="font-mono text-indigo-400 font-bold">{{ remainingTokens() }} / {{ currentQuota().burst }}</span>
              </div>
              <div class="w-full bg-slate-800 rounded-full h-3 overflow-hidden">
                <div class="bg-gradient-to-r from-indigo-500 to-cyan-400 h-3 rounded-full transition-all duration-300"
                     [style.width.%]="tokenPercentage()"></div>
              </div>
              <div class="flex justify-between text-[11px] text-slate-400">
                <span>Refill Rate: {{ currentQuota().refillPerSec | number:'1.1-2' }} tokens/sec</span>
                <span>Limit: {{ currentQuota().rpm }} req/min</span>
              </div>
            </div>

            <!-- 429 Alert Banner -->
            @if (isRateLimited()) {
              <div class="p-3 bg-rose-500/10 border border-rose-500/30 rounded-xl text-xs text-rose-300 flex items-center gap-2 animate-pulse">
                <span>⚠️</span>
                <div>
                  <span class="font-bold">429 Too Many Requests:</span> Quota exhausted. Retry in
                  <span class="font-mono font-bold">{{ retryAfterSeconds() }}s</span>.
                </div>
              </div>
            }

            <!-- Simulation Buttons -->
            <div class="grid grid-cols-3 gap-2 pt-2">
              <button (click)="consumeTokens(1)"
                      class="px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium rounded-lg border border-slate-700 transition">
                1 Token
              </button>
              <button (click)="consumeTokens(10)"
                      class="px-3 py-2 bg-indigo-600/30 hover:bg-indigo-600/50 text-indigo-200 text-xs font-medium rounded-lg border border-indigo-500/40 transition">
                Burst 10
              </button>
              <button (click)="consumeTokens(30)"
                      class="px-3 py-2 bg-rose-600/30 hover:bg-rose-600/50 text-rose-200 text-xs font-medium rounded-lg border border-rose-500/40 transition">
                Flood 30
              </button>
            </div>
          </div>

          <div class="pt-4 border-t border-slate-800 flex justify-between items-center text-[11px] text-slate-400">
            <span>Tenant: <span class="font-mono text-slate-300">{{ tenantId() }}</span></span>
            <button (click)="refillBucket()" class="text-indigo-400 hover:text-indigo-300 underline">Reset Bucket</button>
          </div>
        </div>

        <!-- Right: Sandbox Mock Execution & Simulator (7 cols) -->
        <div class="lg:col-span-7 bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-base font-semibold text-white flex items-center gap-2">
              <span>🧪</span> Mock Conversion & CAPI Simulator
            </h2>
            <div class="flex items-center gap-2">
              <label class="text-xs text-slate-400">Force Error:</label>
              <input type="checkbox" [(ngModel)]="forceFailure" class="rounded bg-slate-800 border-slate-700 text-indigo-600 focus:ring-0">
            </div>
          </div>

          <!-- Controls: Endpoint & Latency -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs text-slate-400 mb-1">Target Endpoint</label>
              <select [(ngModel)]="selectedEndpoint"
                      class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
                <option value="/api/v1/advertising/capi/events">POST /advertising/capi/events</option>
                <option value="/api/v1/customer-data/identity/stitch">POST /customer-data/identity/stitch</option>
                <option value="/api/v1/developer/webhooks/dispatch">POST /developer/webhooks/dispatch</option>
              </select>
            </div>
            <div>
              <label class="block text-xs text-slate-400 mb-1">Simulated Latency ({{ simulatedLatency() }}ms)</label>
              <input type="range" min="10" max="500" step="10" [(ngModel)]="simulatedLatency"
                     class="w-full accent-indigo-500 bg-slate-800 h-2 rounded-lg cursor-pointer">
            </div>
          </div>

          <!-- Action Button -->
          <div class="flex justify-end">
            <button (click)="executeSimulation()"
                    class="px-4 py-2 bg-gradient-to-r from-indigo-600 to-cyan-500 hover:from-indigo-500 hover:to-cyan-400 text-white text-xs font-semibold rounded-lg shadow-md transition">
              Dispatch Simulated Request ⚡
            </button>
          </div>

          <!-- Output Inspector -->
          @if (lastSimulation()) {
            <div class="p-4 bg-slate-950/90 rounded-xl border border-slate-800 space-y-2">
              <div class="flex items-center justify-between text-xs">
                <div class="flex items-center gap-2">
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                        [ngClass]="lastSimulation()!.statusCode === 200 ? 'bg-emerald-500/20 text-emerald-400' : 'bg-rose-500/20 text-rose-400'">
                    {{ lastSimulation()!.statusCode }}
                  </span>
                  <span class="text-slate-400 font-mono">{{ lastSimulation()!.traceId }}</span>
                </div>
                <span class="text-slate-400 font-mono">{{ lastSimulation()!.latencyMs }}ms</span>
              </div>
              <pre class="text-[11px] font-mono text-cyan-300 bg-slate-900/50 p-2.5 rounded border border-slate-800/80 overflow-x-auto">{{ lastSimulation()!.responsePayload }}</pre>
            </div>
          }
        </div>
      </div>

      <!-- Bottom: Dynamic OpenAPI 3.0 Schema Explorer -->
      <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-slate-800 pb-3">
          <div class="flex items-center gap-2">
            <span class="text-lg">📖</span>
            <h3 class="text-sm font-semibold text-white">Role-Partitioned OpenAPI 3.0 Catalog</h3>
          </div>
          <div class="flex items-center gap-2">
            @for (role of ['public', 'partner', 'admin']; track role) {
              <button (click)="selectRole(role)"
                      class="px-3 py-1 rounded text-xs font-medium transition capitalize"
                      [ngClass]="selectedRole() === role ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400 hover:text-slate-200'">
                {{ role }}
              </button>
            }
          </div>
        </div>

        <!-- Endpoints List -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
          @for (ep of visibleEndpoints(); track ep.path) {
            <div class="p-3 bg-slate-950/60 border border-slate-800/80 rounded-xl space-y-1">
              <div class="flex items-center gap-2">
                <span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300 uppercase">
                  {{ ep.method }}
                </span>
                <span class="text-xs font-mono text-slate-200 truncate">{{ ep.path }}</span>
              </div>
              <p class="text-[11px] text-slate-400 line-clamp-1">{{ ep.summary }}</p>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class DeveloperSandboxCockpitComponent {
  translation: TranslationService;

  tiers: DeveloperTier[] = ['Starter', 'Growth', 'Enterprise', 'Partner'];
  activeTier = signal<DeveloperTier>('Enterprise');
  tenantId = signal<string>('tenant_growth_demo');
  isSandboxMode = signal<boolean>(true);
  simulatedLatency = signal<number>(45);
  forceFailure = false;
  selectedEndpoint = '/api/v1/advertising/capi/events';
  selectedRole = signal<string>('public');

  remainingTokens = signal<number>(500);
  isRateLimited = signal<boolean>(false);
  retryAfterSeconds = signal<number>(0);
  lastSimulation = signal<SimulationResult | null>(null);

  private readonly quotas: Record<DeveloperTier, TierQuota> = {
    Starter: { tier: 'Starter', rpm: 100, burst: 25, refillPerSec: 1.67 },
    Growth: { tier: 'Growth', rpm: 500, burst: 100, refillPerSec: 8.33 },
    Enterprise: { tier: 'Enterprise', rpm: 2500, burst: 500, refillPerSec: 41.67 },
    Partner: { tier: 'Partner', rpm: 10000, burst: 2000, refillPerSec: 166.67 }
  };

  currentQuota = computed(() => this.quotas[this.activeTier()]);
  tokenPercentage = computed(() => Math.min(100, Math.round((this.remainingTokens() / this.currentQuota().burst) * 100)));

  allEndpoints = [
    { method: 'POST', path: '/api/v1/customer-data/identity/stitch', summary: 'CDP Identity Graph Resolution', role: 'public' },
    { method: 'POST', path: '/api/v1/advertising/capi/events', summary: 'Meta & Google CAPI Ingestion', role: 'public' },
    { method: 'POST', path: '/api/v1/customer-data/firmographics/deanonymize', summary: 'B2B Reverse IP Deanonymization', role: 'public' },
    { method: 'POST', path: '/api/v1/journeys/omnichannel/dispatch', summary: 'Omnichannel Fallback Ladder', role: 'partner' },
    { method: 'POST', path: '/api/v1/agents/growth-director/decompose', summary: 'Autonomous Growth Director Planner', role: 'admin' }
  ];

  visibleEndpoints = computed(() => {
    const role = this.selectedRole();
    if (role === 'admin') return this.allEndpoints;
    if (role === 'partner') return this.allEndpoints.filter(e => e.role === 'public' || e.role === 'partner');
    return this.allEndpoints.filter(e => e.role === 'public');
  });

  constructor(translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
  }

  selectTier(tier: DeveloperTier): void {
    this.activeTier.set(tier);
    this.remainingTokens.set(this.quotas[tier].burst);
    this.isRateLimited.set(false);
  }

  toggleSandboxMode(): void {
    this.isSandboxMode.update(val => !val);
  }

  consumeTokens(count: number): void {
    const current = this.remainingTokens();
    if (current >= count) {
      this.remainingTokens.set(current - count);
      this.isRateLimited.set(false);
    } else {
      this.isRateLimited.set(true);
      const refill = this.currentQuota().refillPerSec;
      const deficit = count - current;
      this.retryAfterSeconds.set(Math.ceil(deficit / refill));
    }
  }

  refillBucket(): void {
    this.remainingTokens.set(this.currentQuota().burst);
    this.isRateLimited.set(false);
    this.retryAfterSeconds.set(0);
  }

  selectRole(role: string): void {
    this.selectedRole.set(role);
  }

  executeSimulation(): void {
    const isError = this.forceFailure;
    const latency = this.simulatedLatency();
    const statusCode = isError ? 500 : 200;
    const traceId = 'sbx_' + Math.random().toString(36).substring(2, 10);

    let payload: string;
    if (isError) {
      payload = JSON.stringify({ error: 'SimulatedSandboxFailure', message: 'Sandbox forced failure mode is active' }, null, 2);
    } else if (this.selectedEndpoint.includes('capi')) {
      payload = JSON.stringify({ status: 'simulated_success', events_received: 1, matched_profiles: 1, latency_ms: latency }, null, 2);
    } else if (this.selectedEndpoint.includes('identity')) {
      payload = JSON.stringify({ status: 'stitched_in_sandbox', cluster_id: 'sbx_clust_8112', confidence: 0.96 }, null, 2);
    } else {
      payload = JSON.stringify({ status: 'delivered_to_sandbox_sink', delivered: true, timestamp: new Date().toISOString() }, null, 2);
    }

    this.lastSimulation.set({
      statusCode,
      latencyMs: latency,
      traceId,
      responsePayload: payload,
      timestamp: new Date().toLocaleTimeString()
    });
  }
}
