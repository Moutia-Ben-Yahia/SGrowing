import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface HealthProbeTelemetry {
  probeId: string;
  resourceName: string;
  resourceType: string;
  latencyMs: number;
  saturationPercent: number;
  status: 'Healthy' | 'Degraded' | 'CriticalUnresponsive';
}

export interface RemediationActionLog {
  actionId: string;
  resourceName: string;
  triggerCondition: string;
  actionTaken: string;
  resultStatus: string;
  recoveryTimeMs: number;
}

@Component({
  selector: 'app-self-healing-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="self-healing-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-sky-400">
            <span class="inline-block w-2 h-2 rounded-full bg-sky-400 animate-pulse"></span>
            Cloud Reliability & Chaos Engineering • Horizon 55
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Infrastructure Self-Healing & Chaos Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-time latency probes, automated circuit-breaker failover, read-replica promotion & chaos resilience testing.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="triggerAutomatedRemediation()"
            data-testid="trigger-remediation-btn"
            class="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-sky-600/20"
          >
            <span>⚡</span> Trigger Self-Healing
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Platform Availability</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-availability">99.994%</div>
          <div class="text-slate-400 text-xs mt-1">Four-Nines High Availability</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Mean Time to Recovery</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-mttr">3.8 s</div>
          <div class="text-sky-400 text-xs mt-1">Autonomous Failure Recovery</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Healthy Probes</div>
          <div class="text-2xl font-bold text-sky-400 mt-1" data-testid="stat-healthy-probes">
            {{ healthyProbesCount() }} / {{ probes().length }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">DB, Cache & Gateway Latencies</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Resilience Scorecard</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">A+ Mission-Critical</div>
          <div class="text-slate-400 text-xs mt-1">Chaos Mesh Audited</div>
        </div>
      </div>

      <!-- Main Stage: Probes Monitor & Chaos Engineering Console -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Probes Monitor (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Distributed Health Probes</h2>
            <span class="text-xs text-slate-400">Sub-10ms Heartbeats</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (probe of probes(); track probe.probeId) {
              <div
                class="p-4 hover:bg-slate-700/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'probe-row-' + probe.probeId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ probe.resourceName }}</span>
                    <span
                      [class]="probe.status === 'Healthy' ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-rose-900/60 text-rose-300 border-rose-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border uppercase"
                      [attr.data-testid]="'status-badge-' + probe.probeId"
                    >
                      {{ probe.status }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Type: <span class="text-slate-300 font-mono">{{ probe.resourceType }}</span> • Saturation: <strong class="text-white">{{ probe.saturationPercent }}%</strong>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">RTT Latency</div>
                  <div
                    [class]="probe.latencyMs < 15 ? 'text-emerald-400' : 'text-amber-400'"
                    class="text-base font-extrabold font-mono"
                  >
                    {{ probe.latencyMs }} ms
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Chaos Engineering Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Chaos Injection Console</h3>
              <p class="text-xs text-slate-400">Multi-Region Fault Simulator</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-sky-950 text-sky-300 border border-sky-800">
              Active Mesh
            </span>
          </div>

          <div class="space-y-3 text-xs">
            <div>
              <label class="block text-slate-300 font-bold mb-1">Select Chaos Experiment</label>
              <select
                [(ngModel)]="selectedChaosType"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-white focus:outline-none"
              >
                <option value="ReplicaPartition">Simulate Availability Zone Network Partition</option>
                <option value="LatencyInjection">Inject 250ms Synthetic Egress Latency</option>
                <option value="ConnectionExhaustion">Simulate 10,000 DB Connection Spike</option>
              </select>
            </div>

            <button
              (click)="injectChaosExperiment()"
              data-testid="inject-chaos-btn"
              class="w-full py-2 bg-rose-600 hover:bg-rose-500 text-white font-semibold rounded-lg text-xs shadow transition-colors"
            >
              Inject Chaos & Test Auto-Recovery
            </button>
          </div>

          @if (chaosResult(); as res) {
            <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2 text-xs" data-testid="chaos-result-card">
              <div class="flex items-center justify-between font-bold text-white">
                <span>Experiment Result:</span>
                <span class="text-emerald-400">{{ res.resilienceGrade }}</span>
              </div>
              <div class="text-slate-300 leading-relaxed text-[11px]">
                {{ res.blastRadius }}
              </div>
              <div class="pt-1 flex items-center justify-between text-slate-400 text-[10px]">
                <span>Automated Recovery: <strong class="text-emerald-400">Triggered in 3.4s</strong></span>
                <span>Zero State Drift</span>
              </div>
            </div>
          }

          <!-- Recent Remediation Log -->
          <div class="pt-3 border-t border-slate-700/60 space-y-2">
            <div class="text-xs font-bold text-slate-300">Latest Autonomous Remediations:</div>
            <div class="p-2.5 bg-slate-900/60 rounded-lg border border-slate-800 text-[11px] text-slate-300 space-y-1 font-mono">
              <div class="text-emerald-400">✓ Recycled idle PostgreSQL connections (340ms)</div>
              <div class="text-sky-400">✓ Warmed secondary Frankfurt read-replica</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SelfHealingMeshCockpitComponent {
  readonly probes = signal<HealthProbeTelemetry[]>([
    {
      probeId: 'prb_pg_primary',
      resourceName: 'PostgreSQL Primary Connection Pool',
      resourceType: 'DatabasePool',
      latencyMs: 8,
      saturationPercent: 42.0,
      status: 'Healthy'
    },
    {
      probeId: 'prb_redis_cluster',
      resourceName: 'Redis L2 Global Distributed Cache',
      resourceType: 'RedisCache',
      latencyMs: 3,
      saturationPercent: 28.5,
      status: 'Healthy'
    },
    {
      probeId: 'prb_gateway_outbound',
      resourceName: 'External Meta CAPI / WhatsApp Egress Gateway',
      resourceType: 'ExternalGateway',
      latencyMs: 48,
      saturationPercent: 64.0,
      status: 'Healthy'
    }
  ]);

  readonly chaosResult = signal<{ blastRadius: string; resilienceGrade: string } | null>({
    blastRadius: 'Simulated network partition across Frankfurt availability zone. Automated quorum promoted read-replica in 3.4 seconds with zero transaction loss.',
    resilienceGrade: 'A+ Resilience'
  });

  selectedChaosType = 'ReplicaPartition';

  readonly healthyProbesCount = computed(() =>
    this.probes().filter(p => p.status === 'Healthy').length
  );

  triggerAutomatedRemediation(): void {
    this.probes.update(list =>
      list.map(p => ({ ...p, status: 'Healthy', latencyMs: Math.max(3, p.latencyMs - 5) }))
    );
  }

  injectChaosExperiment(): void {
    this.chaosResult.set({
      blastRadius: `Chaos experiment [${this.selectedChaosType}] executed. Backpressure token-bucket held load, and self-healing arbiter recovered the pipeline in 3.2s.`,
      resilienceGrade: 'A+ Resilience'
    });
  }
}
