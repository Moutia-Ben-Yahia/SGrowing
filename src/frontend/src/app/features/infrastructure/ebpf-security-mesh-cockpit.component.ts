import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface EbpfProbeNodeRecord {
  probeId: string;
  nodeName: string;
  kernelRelease: string;
  hookedSyscallsCount: number;
  inspectionLatencyMicroseconds: number;
  status: string;
}

export interface SocketViolationAlertRecord {
  alertId: string;
  sourcePod: string;
  targetPod: string;
  targetPort: number;
  protocol: string;
  enforcementAction: string;
  anomalyReason: string;
}

@Component({
  selector: 'app-ebpf-security-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="ebpf-security-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-rose-500 animate-pulse"></span>
            Zero-Trust eBPF Deep Packet Inspection & Kernel Firewall Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Linux Kernel Ring Buffer Socket Hooking, Sub-50μs Lateral Threat Drop & Zero-Overhead Protocol Enforcement
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="simulateLateralThreatBlock()" class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white font-medium rounded-lg shadow-sm transition">
            Simulate Lateral Socket Block
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Probes</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ probes().length }}</p>
          <span class="text-xs text-rose-400">Kernel Syscall Tracepoints</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Inspected Packets</span>
          <p class="text-2xl font-extrabold text-white mt-1">1,420M</p>
          <span class="text-xs text-cyan-400">Zero Proxy Overhead</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Mean Latency</span>
          <p class="text-2xl font-extrabold text-white mt-1">24.5μs</p>
          <span class="text-xs text-emerald-400">Sub-50μs Kernel Filter</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Threats Blocked</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ violations().length }}</p>
          <span class="text-xs text-amber-400">100% Kernel Dropped</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Kernel Probes -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Kernel eBPF Probing Nodes</h2>
          <div class="space-y-4">
            <div *ngFor="let p of probes()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ p.nodeName }}</span>
                <span class="text-xs px-2 py-0.5 rounded bg-rose-950 text-rose-300 border border-rose-800 font-mono">
                  {{ p.status }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Kernel: {{ p.kernelRelease }}</p>
              <div class="flex justify-between text-xs text-slate-300">
                <span>Hooked Syscalls: {{ p.hookedSyscallsCount }}</span>
                <span class="font-mono text-emerald-400 font-bold">{{ p.inspectionLatencyMicroseconds }}μs Latency</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Socket Violations -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Kernel-Dropped Lateral Violations</h2>
          <div class="space-y-4">
            <div *ngFor="let v of violations()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-rose-400">{{ v.sourcePod }} → {{ v.targetPod }}:{{ v.targetPort }}</span>
                <span class="text-xs font-mono font-bold text-rose-300 bg-rose-950 px-2 py-0.5 rounded border border-rose-800">
                  {{ v.enforcementAction }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Protocol: {{ v.protocol }}</p>
              <div class="p-2.5 bg-rose-950/30 border border-rose-800/40 rounded text-xs text-rose-200">
                Anomaly: {{ v.anomalyReason }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class EbpfSecurityMeshCockpitComponent {
  probes = signal<EbpfProbeNodeRecord[]>([
    { probeId: 'EBPF-PRB-EU-01', nodeName: 'k8s-node-worker-eu-central-1a', kernelRelease: '6.8.0-1018-aws (Linux x86_64)', hookedSyscallsCount: 128, inspectionLatencyMicroseconds: 24.5, status: 'PROBING_ACTIVE' },
    { probeId: 'EBPF-PRB-US-02', nodeName: 'k8s-node-worker-us-east-1b', kernelRelease: '6.8.0-1018-aws (Linux x86_64)', hookedSyscallsCount: 128, inspectionLatencyMicroseconds: 26.2, status: 'PROBING_ACTIVE' }
  ]);

  violations = signal<SocketViolationAlertRecord[]>([
    { alertId: 'VIO-EBPF-401', sourcePod: 'sbx-agent-scrape-4411', targetPod: 'core-postgres-primary', targetPort: 5432, protocol: 'TCP_POSTGRES', enforcementAction: 'KERNEL_DROP', anomalyReason: 'Untrusted sandboxed agent attempted direct raw TCP connect to primary DB without mTLS proxy' }
  ]);

  simulateLateralThreatBlock() {
    const newVio: SocketViolationAlertRecord = {
      alertId: `VIO-EBPF-${Date.now().toString().slice(-4)}`,
      sourcePod: 'untrusted-crawler-pod-88',
      targetPod: 'internal-vault-service',
      targetPort: 8200,
      protocol: 'TCP_RAW',
      enforcementAction: 'KERNEL_DROP',
      anomalyReason: 'Anomalous lateral movement attempt without JWT identity attestation'
    };
    this.violations.update(list => [newVio, ...list]);
  }
}
