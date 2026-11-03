import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ChaosExperiment {
  experimentId: string;
  targetServiceName: string;
  region: string;
  fault: 'LatencyInjection' | 'PacketLossSpike' | 'RegionFailoverSimulation' | 'MemoryPressureSpike';
  magnitudePercentage: number;
  state: 'Idle' | 'InjectingFault' | 'RecoveringSelfHealing' | 'HaltedEmergencyStop';
  currentLatencyMs: number;
  steadyStateLatencyMs: number;
}

@Component({
  selector: 'app-chaos-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="chaos-mesh-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-red-500/20 text-red-400 border border-red-500/30">Horizon 95</span>
            <span class="text-xs text-slate-400">Chaos Engineering & Fault Injection</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Multi-Region Chaos Engineering Mesh</h1>
          <p class="text-sm text-slate-400">Controlled fault injection, blast-radius containment, self-healing MTTR validation, and emergency kill switches.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-experiments">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Fault Injections</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ activeExperimentsCount() }} In Flight</div>
          <div class="text-xs text-slate-400 mt-1">Multi-region isolated blast radius</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-system-availability">
          <div class="text-xs font-medium text-slate-400 uppercase">Mesh Availability SLO</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">99.995%</div>
          <div class="text-xs text-slate-400 mt-1">Zero user-facing degradation</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-average-mttr">
          <div class="text-xs font-medium text-slate-400 uppercase">Mean Time to Recover (MTTR)</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">420ms</div>
          <div class="text-xs text-slate-400 mt-1">Autonomous mesh rerouting</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-halted-experiments">
          <div class="text-xs font-medium text-slate-400 uppercase">Halted / Safe State</div>
          <div class="text-2xl font-bold text-white mt-1">{{ haltedCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Emergency kill-switched or idle</div>
        </div>
      </section>

      <!-- Experiments Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Chaos Experiment Topology</h2>
          <div class="space-y-3">
            @for (exp of experiments(); track exp.experimentId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedExperiment()?.experimentId === exp.experimentId"
                [class.ring-red-500]="selectedExperiment()?.experimentId === exp.experimentId"
                (click)="selectExperiment(exp)"
                data-testid="chaos-experiment-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="font-semibold text-white text-base">{{ exp.targetServiceName }} ({{ exp.region }})</h3>
                    <p class="text-xs text-slate-400 mt-0.5">Fault: {{ exp.fault }} &bull; Magnitude: {{ exp.magnitudePercentage }}%</p>
                  </div>
                  <div class="text-right">
                    <span class="text-xs px-2.5 py-1 rounded font-medium"
                      [class.bg-amber-500/20]="exp.state === 'InjectingFault'"
                      [class.text-amber-400]="exp.state === 'InjectingFault'"
                      [class.bg-sky-500/20]="exp.state === 'RecoveringSelfHealing'"
                      [class.text-sky-400]="exp.state === 'RecoveringSelfHealing'"
                      [class.bg-slate-700]="exp.state === 'Idle'"
                      [class.text-slate-300]="exp.state === 'Idle'"
                      [class.bg-rose-500/20]="exp.state === 'HaltedEmergencyStop'"
                      [class.text-rose-400]="exp.state === 'HaltedEmergencyStop'"
                    >
                      {{ exp.state }}
                    </span>
                    <div class="text-xs text-slate-300 mt-1">Latency: {{ exp.currentLatencyMs | number:'1.0-0' }}ms (Base: {{ exp.steadyStateLatencyMs }}ms)</div>
                  </div>
                </div>

                <div class="mt-4 flex items-center justify-between pt-2 border-t border-slate-700/60">
                  <span class="text-xs text-slate-400">Blast Radius: <strong class="text-emerald-400">Canary Tier 5%</strong></span>
                  @if (exp.state === 'InjectingFault') {
                    <button
                      (click)="haltExperiment(exp.experimentId); $event.stopPropagation()"
                      data-testid="btn-halt-chaos"
                      class="px-3 py-1 bg-rose-700 hover:bg-rose-600 text-white text-xs font-semibold rounded shadow transition-colors"
                    >
                      Emergency Halt
                    </button>
                  } @else {
                    <span class="text-xs text-emerald-400 font-semibold" data-testid="steady-state-badge">
                      Steady State Restored
                    </span>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Resilience Probe Telemetry</h2>
            @if (selectedExperiment(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Target Probe Service</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.targetServiceName }} ({{ sel.region }})</div>
                  <div class="text-xs text-rose-400 mt-1">Experiment ID: {{ sel.experimentId }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Current Latency:</span>
                    <span class="font-mono text-amber-300">{{ sel.currentLatencyMs | number:'1.0-0' }} ms</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Baseline Latency:</span>
                    <span class="font-mono text-emerald-400">{{ sel.steadyStateLatencyMs }} ms</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Circuit Breaker Status:</span>
                    <span class="font-mono text-emerald-400">Tripped & Rerouted</span>
                  </div>
                </div>

                @if (sel.state === 'InjectingFault') {
                  <button
                    (click)="haltExperiment(sel.experimentId)"
                    class="w-full mt-4 py-2 bg-gradient-to-r from-rose-600 to-red-600 hover:from-rose-500 hover:to-red-500 text-white font-semibold rounded text-sm shadow transition-all"
                  >
                    Trigger Emergency Kill Switch
                  </button>
                }
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select an experiment to view resilience telemetry.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-rose-400 font-semibold">Automated Safety Guard:</span> If p99 latency breaches 250ms or errors exceed 0.05%, experiment halts automatically.
          </div>
        </div>
      </section>
    </div>
  `
})
export class ChaosMeshCockpitComponent {
  experiments = signal<ChaosExperiment[]>([
    {
      experimentId: 'exp_chaos_01',
      targetServiceName: 'api-gateway',
      region: 'us-east-1',
      fault: 'LatencyInjection',
      magnitudePercentage: 40,
      state: 'InjectingFault',
      currentLatencyMs: 185.0,
      steadyStateLatencyMs: 22.0
    },
    {
      experimentId: 'exp_chaos_02',
      targetServiceName: 'shard-broker',
      region: 'eu-central-1',
      fault: 'PacketLossSpike',
      magnitudePercentage: 15,
      state: 'RecoveringSelfHealing',
      currentLatencyMs: 34.0,
      steadyStateLatencyMs: 18.0
    },
    {
      experimentId: 'exp_chaos_03',
      targetServiceName: 'auth-vault',
      region: 'ap-southeast-1',
      fault: 'RegionFailoverSimulation',
      magnitudePercentage: 100,
      state: 'Idle',
      currentLatencyMs: 14.0,
      steadyStateLatencyMs: 14.0
    }
  ]);

  selectedExperiment = signal<ChaosExperiment | null>(this.experiments()[0]);

  activeExperimentsCount = computed(() =>
    this.experiments().filter(e => e.state === 'InjectingFault').length
  );

  haltedCount = computed(() =>
    this.experiments().filter(e => e.state === 'HaltedEmergencyStop' || e.state === 'Idle').length
  );

  selectExperiment(experiment: ChaosExperiment): void {
    this.selectedExperiment.set(experiment);
  }

  haltExperiment(experimentId: string): void {
    this.experiments.update(list =>
      list.map(e => {
        if (e.experimentId === experimentId) {
          return {
            ...e,
            state: 'HaltedEmergencyStop',
            currentLatencyMs: e.steadyStateLatencyMs
          };
        }
        return e;
      })
    );

    if (this.selectedExperiment()?.experimentId === experimentId) {
      this.selectedExperiment.update(curr => {
        if (!curr) return null;
        return {
          ...curr,
          state: 'HaltedEmergencyStop',
          currentLatencyMs: curr.steadyStateLatencyMs
        };
      });
    }
  }
}
