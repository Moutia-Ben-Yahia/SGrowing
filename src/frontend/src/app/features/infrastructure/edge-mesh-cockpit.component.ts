import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface EdgePoPNode {
  popCode: string;
  locationName: string;
  region: 'NorthAmericaEast' | 'NorthAmericaWest' | 'EuropeWest' | 'AsiaEast' | 'Oceania' | 'SouthAmerica';
  p95LatencyMs: number;
  cpuLoadPct: number;
  activeIsolatesCount: number;
  isHealthy: boolean;
}

export interface ServerlessEdgeFunction {
  functionId: string;
  functionName: string;
  runtime: string;
  canaryTrafficSplitPct: number;
  deploymentState: 'Synchronized' | 'CanaryRolling' | 'PromotedGA';
  deployedAt: string;
  sha256Hash: string;
}

@Component({
  selector: 'app-edge-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="edge-mesh-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-rose-500/20 text-rose-400 border border-rose-500/30">Horizon 100</span>
            <span class="px-2 py-0.5 text-xs font-bold rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">Centennial Milestone</span>
            <span class="text-xs text-slate-400">Global Edge Compute & Geo-Distributed Serverless Mesh</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Global Edge Compute Mesh & Serverless Execution Plane</h1>
          <p class="text-sm text-slate-400">Sub-5ms V8 isolate execution plane across 6 global Tier-1 Edge PoPs, zero-downtime canary distribution, and latency self-routing.</p>
        </div>
      </header>

      <!-- KPI Overview Cards -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-pops">
          <div class="text-xs font-medium text-slate-400 uppercase">Global Edge PoPs</div>
          <div class="text-2xl font-bold text-white mt-1">{{ healthyPoPsCount() }} / {{ pops().length }} Active</div>
          <div class="text-xs text-slate-400 mt-1">Tier-1 edge data centers</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-global-latency">
          <div class="text-xs font-medium text-slate-400 uppercase">Global P95 Edge Latency</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ globalP95Latency() | number:'1.1-1' }} ms</div>
          <div class="text-xs text-slate-400 mt-1">Anycast DNS roundtrip latency</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-global-rps">
          <div class="text-xs font-medium text-slate-400 uppercase">Global Request Rate</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">284.5k req/s</div>
          <div class="text-xs text-slate-400 mt-1">Sustained peak isolate throughput</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-isolates">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Warm Isolates</div>
          <div class="text-2xl font-bold text-purple-400 mt-1">{{ totalWarmIsolates() }} Isolates</div>
          <div class="text-xs text-slate-400 mt-1">&lt;3ms cold start across PoPs</div>
        </div>
      </section>

      <!-- Edge PoP Nodes Grid -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Tier-1 Global Edge PoPs Topology</h2>
          <span class="text-xs text-slate-400">Mesh Protocol: WireGuard BGP Anycast</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-6 gap-3">
          @for (p of pops(); track p.popCode) {
            <div class="bg-slate-900/70 border p-3.5 rounded-lg flex flex-col justify-between"
                 [ngClass]="p.isHealthy ? 'border-slate-750' : 'border-rose-500/50 bg-rose-950/20'"
                 [attr.data-testid]="'pop-card-' + p.popCode">
              <div>
                <div class="flex items-center justify-between mb-1.5">
                  <span class="text-xs font-mono font-bold text-white">{{ p.popCode }}</span>
                  <span class="w-2.5 h-2.5 rounded-full"
                        [ngClass]="p.isHealthy ? 'bg-emerald-400' : 'bg-rose-500 animate-pulse'"></span>
                </div>
                <div class="text-[11px] text-slate-300 font-medium mb-2 leading-tight">{{ p.locationName }}</div>

                <div class="space-y-1 text-[11px] font-mono text-slate-400">
                  <div class="flex justify-between">
                    <span>p95 Latency:</span>
                    <span class="font-bold" [ngClass]="p.p95LatencyMs < 8 ? 'text-emerald-400' : 'text-amber-400'">
                      {{ p.p95LatencyMs }}ms
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span>CPU Load:</span>
                    <span class="text-slate-200">{{ p.cpuLoadPct }}%</span>
                  </div>
                  <div class="flex justify-between">
                    <span>Isolates:</span>
                    <span class="text-slate-200">{{ p.activeIsolatesCount }}</span>
                  </div>
                </div>
              </div>

              <button class="mt-3 w-full py-1 text-[10px] font-semibold rounded transition-colors"
                      [ngClass]="p.isHealthy ? 'bg-slate-800 hover:bg-slate-700 text-slate-300' : 'bg-emerald-600 hover:bg-emerald-500 text-white'"
                      (click)="toggleHealth(p.popCode)"
                      [attr.data-testid]="'btn-toggle-' + p.popCode">
                {{ p.isHealthy ? 'Simulate Fault' : 'Restore PoP' }}
              </button>
            </div>
          }
        </div>
      </section>

      <!-- Deployed Serverless Edge Functions Table -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Geo-Distributed Serverless Edge Functions</h2>
          <span class="text-xs text-slate-400">Zero-Downtime Rollout</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="functions-table">
            <thead class="text-xs uppercase bg-slate-800 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Function ID / Name</th>
                <th class="py-3 px-4">Runtime</th>
                <th class="py-3 px-4">Canary Split</th>
                <th class="py-3 px-4">Deployment Status</th>
                <th class="py-3 px-4">SHA-256 Digest</th>
                <th class="py-3 px-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-750 text-slate-300">
              @for (fn of functions(); track fn.functionId) {
                <tr class="hover:bg-slate-800/50 transition-colors" [attr.data-testid]="'fn-row-' + fn.functionId">
                  <td class="py-3 px-4">
                    <div class="font-medium text-white">{{ fn.functionName }}</div>
                    <div class="text-xs text-slate-400 font-mono">{{ fn.functionId }}</div>
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-mono bg-slate-750 text-slate-300">{{ fn.runtime }}</span>
                  </td>
                  <td class="py-3 px-4">
                    <div class="flex items-center gap-2">
                      <div class="w-24 bg-slate-700 rounded-full h-2">
                        <div class="bg-cyan-400 h-2 rounded-full" [style.width.%]="fn.canaryTrafficSplitPct"></div>
                      </div>
                      <span class="text-xs font-mono font-bold text-white">{{ fn.canaryTrafficSplitPct }}%</span>
                    </div>
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-medium"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-400': fn.deploymentState === 'PromotedGA',
                            'bg-cyan-500/20 text-cyan-400': fn.deploymentState === 'CanaryRolling',
                            'bg-slate-700 text-slate-300': fn.deploymentState === 'Synchronized'
                          }">
                      {{ fn.deploymentState }}
                    </span>
                  </td>
                  <td class="py-3 px-4 text-xs font-mono text-slate-400">
                    {{ fn.sha256Hash }}
                  </td>
                  <td class="py-3 px-4 text-right">
                    @if (fn.canaryTrafficSplitPct < 100) {
                      <button class="px-3 py-1 text-xs font-semibold rounded bg-cyan-600 hover:bg-cyan-500 text-white transition-colors"
                              (click)="promote(fn.functionId)"
                              [attr.data-testid]="'btn-promote-' + fn.functionId">
                        Promote to 100% GA
                      </button>
                    } @else {
                      <span class="text-xs text-emerald-400 font-medium">100% Worldwide</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Action Toast -->
      @if (meshNotice()) {
        <div class="p-3 bg-cyan-950/60 border border-cyan-500/40 rounded-xl text-cyan-300 text-xs flex items-center justify-between"
             data-testid="mesh-notice">
          <span>{{ meshNotice() }}</span>
          <button (click)="meshNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class EdgeMeshCockpitComponent {
  readonly pops = signal<EdgePoPNode[]>([
    { popCode: 'IAD-01', locationName: 'N. Virginia (US-East)', region: 'NorthAmericaEast', p95LatencyMs: 4.2, cpuLoadPct: 28.5, activeIsolatesCount: 342, isHealthy: true },
    { popCode: 'SFO-02', locationName: 'San Francisco (US-West)', region: 'NorthAmericaWest', p95LatencyMs: 5.8, cpuLoadPct: 32.1, activeIsolatesCount: 289, isHealthy: true },
    { popCode: 'FRA-01', locationName: 'Frankfurt (Europe-Central)', region: 'EuropeWest', p95LatencyMs: 6.1, cpuLoadPct: 35.4, activeIsolatesCount: 310, isHealthy: true },
    { popCode: 'NRT-01', locationName: 'Tokyo Narita (Asia-NE)', region: 'AsiaEast', p95LatencyMs: 7.4, cpuLoadPct: 29.8, activeIsolatesCount: 240, isHealthy: true },
    { popCode: 'SYD-01', locationName: 'Sydney (Oceania)', region: 'Oceania', p95LatencyMs: 11.2, cpuLoadPct: 22.0, activeIsolatesCount: 180, isHealthy: true },
    { popCode: 'GRU-01', locationName: 'São Paulo (South America)', region: 'SouthAmerica', p95LatencyMs: 14.5, cpuLoadPct: 26.4, activeIsolatesCount: 155, isHealthy: true }
  ]);

  readonly functions = signal<ServerlessEdgeFunction[]>([
    {
      functionId: 'EDGE-FN-01',
      functionName: 'Dynamic-Geo-Personalization-Gate',
      runtime: 'V8-Isolate-ES2026',
      canaryTrafficSplitPct: 100,
      deploymentState: 'PromotedGA',
      deployedAt: '2026-10-31T00:00:00Z',
      sha256Hash: 'SHA256:7B94F3C8A012'
    },
    {
      functionId: 'EDGE-FN-02',
      functionName: 'Zero-Trust-JWT-Token-Introspection',
      runtime: 'Wasm-Rust-v1',
      canaryTrafficSplitPct: 50,
      deploymentState: 'CanaryRolling',
      deployedAt: '2026-11-02T22:00:00Z',
      sha256Hash: 'SHA256:4C82E11D9A77'
    },
    {
      functionId: 'EDGE-FN-03',
      functionName: 'CAPI-Payload-Transform-Streamer',
      runtime: 'V8-Isolate-ES2026',
      canaryTrafficSplitPct: 10,
      deploymentState: 'CanaryRolling',
      deployedAt: '2026-11-03T01:30:00Z',
      sha256Hash: 'SHA256:1E99F82A33BC'
    }
  ]);

  readonly meshNotice = signal<string | null>(null);

  readonly healthyPoPsCount = computed(() =>
    this.pops().filter(p => p.isHealthy).length
  );

  readonly globalP95Latency = computed(() => {
    const list = this.pops().filter(p => p.isHealthy);
    if (list.length === 0) return 0;
    return list.reduce((sum, p) => sum + p.p95LatencyMs, 0) / list.length;
  });

  readonly totalWarmIsolates = computed(() =>
    this.pops().reduce((sum, p) => sum + p.activeIsolatesCount, 0)
  );

  toggleHealth(popCode: string): void {
    this.pops.update(list =>
      list.map(p => p.popCode === popCode ? { ...p, isHealthy: !p.isHealthy } : p)
    );
    const updated = this.pops().find(p => p.popCode === popCode);
    this.meshNotice.set(`PoP ${popCode} is now ${updated?.isHealthy ? 'Healthy & Online' : 'Degraded / Simulating Outage'}.`);
  }

  promote(functionId: string): void {
    this.functions.update(list =>
      list.map(fn => fn.functionId === functionId ? {
        ...fn,
        canaryTrafficSplitPct: 100,
        deploymentState: 'PromotedGA'
      } : fn)
    );
    this.meshNotice.set(`Function ${functionId} promoted to 100% GA across all global Tier-1 Edge PoPs.`);
  }
}
