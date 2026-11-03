import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SandboxedPodRecord {
  podName: string;
  namespaceName: string;
  runtimeClass: string;
  assignedAgentId: string;
  memoryLimitMb: number;
  cpuLimitMillicores: number;
  nodeName: string;
  status: string;
}

export interface ChaosEvictionRecord {
  evictionId: string;
  podName: string;
  sourceNode: string;
  targetNode: string;
  reschedulingLatencyMs: number;
  evictionReason: string;
}

@Component({
  selector: 'app-pod-sandboxing-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="pod-sandboxing-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-rose-500 animate-pulse"></span>
            Zero-Trust Kubernetes Pod Sandboxing & Chaos Resilience Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            gVisor Kernel Sandbox Isolation, Kata MicroVM Containers & Sub-Second Zero-Loss Pod Chaos Rescheduling
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="triggerChaosDrill()" class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white font-medium rounded-lg shadow-sm transition">
            Simulate Chaos Node Drain Drill
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Isolated Pods</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ pods().length }}</p>
          <span class="text-xs text-rose-400">gVisor / Kata MicroVMs</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">24h Sandboxes</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">1,240</p>
          <span class="text-xs text-slate-400">Untrusted AI Tasks</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Zero-Loss Reschedule</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">100.0%</p>
          <span class="text-xs text-slate-400">State Preserved</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Start Latency</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">145ms</p>
          <span class="text-xs text-emerald-400">MicroVM Fast Boot</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Sandboxed Pods Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Isolated Pod Workloads</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Pod Name</th>
                  <th class="pb-3 font-medium">Runtime Class</th>
                  <th class="pb-3 font-medium">Limits (RAM/CPU)</th>
                  <th class="pb-3 font-medium">Current Node</th>
                  <th class="pb-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let p of pods()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-rose-300 font-semibold">{{ p.podName }}</td>
                  <td class="py-3 text-slate-300 text-xs">{{ p.runtimeClass }}</td>
                  <td class="py-3 font-mono text-xs text-slate-400">{{ p.memoryLimitMb }}MB / {{ p.cpuLimitMillicores }}m</td>
                  <td class="py-3 font-mono text-slate-400 text-xs truncate max-w-[140px]">{{ p.nodeName }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-[10px] rounded font-semibold font-mono bg-emerald-500/20 text-emerald-300">
                      {{ p.status }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Chaos Drills Ledger -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Chaos Resilience Drills</h2>
          <div class="space-y-3">
            <div *ngFor="let e of evictions()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-mono text-xs text-rose-400 font-bold">{{ e.evictionId }}</span>
                <span class="px-2 py-0.5 text-xs bg-emerald-500/20 text-emerald-300 rounded font-semibold font-mono">{{ e.reschedulingLatencyMs }}ms</span>
              </div>
              <p class="text-xs text-slate-300">{{ e.evictionReason }}</p>
              <div class="mt-2 text-[10px] font-mono text-slate-400 bg-slate-950 p-1.5 rounded truncate">
                {{ e.sourceNode }} → {{ e.targetNode }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class PodSandboxingMeshCockpitComponent {
  pods = signal<SandboxedPodRecord[]>([
    { podName: 'sbx-agent-scrape-4411', namespaceName: 'sgrowing-sandbox', runtimeClass: 'gVisor (runsc kernel)', assignedAgentId: 'AGT-SCRAPER-01', memoryLimitMb: 2048, cpuLimitMillicores: 2000, nodeName: 'k8s-node-worker-eu-central-1a', status: 'RUNNING_ISOLATED' },
    { podName: 'sbx-agent-coder-9922', namespaceName: 'sgrowing-sandbox', runtimeClass: 'Kata Containers (microVM)', assignedAgentId: 'AGT-CODER-02', memoryLimitMb: 4096, cpuLimitMillicores: 4000, nodeName: 'k8s-node-worker-eu-central-1b', status: 'RUNNING_ISOLATED' }
  ]);

  evictions = signal<ChaosEvictionRecord[]>([
    { evictionId: 'EVT-CHAOS-101', podName: 'sbx-agent-scrape-4411', sourceNode: 'k8s-node-worker-eu-central-1a', targetNode: 'k8s-node-worker-eu-central-1c', reschedulingLatencyMs: 340.0, evictionReason: 'CHAOS_NODE_DRAIN_DRILL' }
  ]);

  triggerChaosDrill() {
    const newEviction: ChaosEvictionRecord = {
      evictionId: `EVT-CHAOS-${Date.now().toString().slice(-4)}`,
      podName: 'sbx-agent-coder-9922',
      sourceNode: 'k8s-node-worker-eu-central-1b',
      targetNode: 'k8s-node-worker-eu-central-1d',
      reschedulingLatencyMs: 285.0,
      evictionReason: 'CHAOS_KERNEL_PANIC_SIMULATION'
    };
    this.evictions.update(list => [newEviction, ...list]);
  }
}
