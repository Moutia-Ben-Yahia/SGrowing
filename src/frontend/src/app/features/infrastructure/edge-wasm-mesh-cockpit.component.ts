import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface EdgeWasmModuleRecord {
  moduleId: string;
  moduleName: string;
  edgeProvider: string;
  bytecodeSizeBytes: number;
  coldStartLatencyMs: number;
  activeEdgePoPsDeployedCount: number;
  status: string;
}

export interface EdgeInvocationMetricRecord {
  invocationId: string;
  moduleId: string;
  edgeLocationIata: string;
  executionDurationMs: number;
  statusCode: number;
}

@Component({
  selector: 'app-edge-wasm-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="edge-wasm-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-cyan-500 animate-pulse"></span>
            Multi-Cloud Edge CDN WebAssembly (Wasm) Micro-Compute Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Global Sub-5ms Serverless Wasm Execution, Cloudflare Workers / Fastly Compute Runtime & Instant Cold-Start Personalization
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="simulateEdgeInvocation()" class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white font-medium rounded-lg shadow-sm transition">
            Simulate Edge PoP Invocation
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Edge Modules</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ modules().length }}</p>
          <span class="text-xs text-cyan-400">Compiled Bytecode</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Global PoPs</span>
          <p class="text-2xl font-extrabold text-white mt-1">310 PoPs</p>
          <span class="text-xs text-emerald-400">Multi-Cloud Edge Mesh</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">P99 Execution</span>
          <p class="text-2xl font-extrabold text-white mt-1">2.8ms</p>
          <span class="text-xs text-amber-400">Sub-5ms Execution SLA</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">24h Volume</span>
          <p class="text-2xl font-extrabold text-white mt-1">18.5M</p>
          <span class="text-xs text-indigo-400">Zero Server Cold Start</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Deployed Wasm Modules -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Active Edge Wasm Bytecode Modules</h2>
          <div class="space-y-4">
            <div *ngFor="let m of modules()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ m.moduleName }}</span>
                <span class="text-xs px-2 py-0.5 rounded font-mono bg-cyan-950 text-cyan-300 border border-cyan-800">
                  {{ m.edgeProvider }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Size: {{ (m.bytecodeSizeBytes / 1024).toFixed(1) }} KB • PoPs: {{ m.activeEdgePoPsDeployedCount }}</p>
              <div class="flex justify-between text-xs text-slate-300 font-mono">
                <span>Cold Start: {{ m.coldStartLatencyMs }}ms</span>
                <span class="text-emerald-400 font-bold">{{ m.status }}</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Edge Invocations -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Real-Time Edge Invocations</h2>
          <div class="space-y-4">
            <div *ngFor="let i of invocations()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-cyan-400">PoP Airport: {{ i.edgeLocationIata }}</span>
                <span class="text-xs font-mono font-bold text-emerald-400">{{ i.executionDurationMs }}ms</span>
              </div>
              <p class="text-xs text-slate-400 mb-1">Module: {{ i.moduleId }}</p>
              <div class="text-[10px] font-mono text-slate-400">
                Invocation ID: {{ i.invocationId }} • HTTP Status: {{ i.statusCode }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class EdgeWasmMeshCockpitComponent {
  modules = signal<EdgeWasmModuleRecord[]>([
    { moduleId: 'WASM-MOD-GEO-01', moduleName: 'edge-geo-personalization-rewrite', edgeProvider: 'CLOUDFLARE_WORKERS', bytecodeSizeBytes: 48520, coldStartLatencyMs: 1.2, activeEdgePoPsDeployedCount: 310, status: 'ACTIVE_GLOBAL' },
    { moduleId: 'WASM-MOD-AUTH-02', moduleName: 'edge-jwt-uid2-token-validator', edgeProvider: 'FASTLY_COMPUTE', bytecodeSizeBytes: 64200, coldStartLatencyMs: 1.8, activeEdgePoPsDeployedCount: 240, status: 'ACTIVE_GLOBAL' }
  ]);

  invocations = signal<EdgeInvocationMetricRecord[]>([
    { invocationId: 'INV-LHR-8812', moduleId: 'WASM-MOD-GEO-01', edgeLocationIata: 'LHR', executionDurationMs: 2.4, statusCode: 200 }
  ]);

  simulateEdgeInvocation() {
    const newInv: EdgeInvocationMetricRecord = {
      invocationId: `INV-SIN-${Date.now().toString().slice(-4)}`,
      moduleId: 'WASM-MOD-AUTH-02',
      edgeLocationIata: 'SIN',
      executionDurationMs: 2.1,
      statusCode: 200
    };
    this.invocations.update(list => [newInv, ...list]);
  }
}
