import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface K8sTenantNamespace {
  namespaceId: string;
  tenantId: string;
  provider: 'AwsEks' | 'GoogleGke' | 'AzureAks';
  clusterName: string;
  level: 'StandardNamespace' | 'HardenedEbpfQuarantine' | 'ZeroTrustMtlsEnforced';
  activePodsCount: number;
  cpuQuotaCores: number;
  memoryQuotaGb: number;
  isQuarantined: boolean;
  createdAt: string;
}

@Component({
  selector: 'app-k8s-isolation-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="k8s-isolation-mesh-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-rose-500/20 text-rose-400 border border-rose-500/30">Horizon 105</span>
            <span class="text-xs text-slate-400">Multi-Cloud Kubernetes & eBPF Security Mesh</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Multi-Cloud Kubernetes Namespace & Secret Isolation Mesh</h1>
          <p class="text-sm text-slate-400">Multi-cloud cluster orchestration (EKS/GKE/AKS), zero-trust eBPF egress/ingress enforcement, and automated quarantine.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-namespaces">
          <div class="text-xs font-medium text-slate-400 uppercase">Tenant Namespaces</div>
          <div class="text-2xl font-bold text-white mt-1">{{ namespaces().length }} Active</div>
          <div class="text-xs text-slate-400 mt-1">Across 3 multi-cloud clusters</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-quarantined">
          <div class="text-xs font-medium text-slate-400 uppercase">eBPF Quarantined</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ quarantinedCount() }} Namespaces</div>
          <div class="text-xs text-slate-400 mt-1">Blocked for network policy anomalies</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-pods">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Isolated Pods</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ totalPods() }} Pods</div>
          <div class="text-xs text-slate-400 mt-1">mTLS cryptographically sealed</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-cluster-load">
          <div class="text-xs font-medium text-slate-400 uppercase">Avg Cluster Quota Load</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">38.5%</div>
          <div class="text-xs text-slate-400 mt-1">CPU/Memory resource headroom</div>
        </div>
      </section>

      <!-- Namespaces Table -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Multi-Cloud Tenant Workload Isolation</h2>
          <span class="text-xs text-slate-400">Zero-Trust Cilium eBPF Plane</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="namespaces-table">
            <thead class="text-xs uppercase bg-slate-800 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Namespace / Tenant</th>
                <th class="py-3 px-4">Cloud Provider & Cluster</th>
                <th class="py-3 px-4">Active Pods</th>
                <th class="py-3 px-4">Resource Quota</th>
                <th class="py-3 px-4">Isolation Level</th>
                <th class="py-3 px-4">Security Posture</th>
                <th class="py-3 px-4 text-right">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-750 text-slate-300">
              @for (ns of namespaces(); track ns.namespaceId) {
                <tr class="hover:bg-slate-800/50 transition-colors" [attr.data-testid]="'namespace-row-' + ns.namespaceId">
                  <td class="py-3 px-4">
                    <div class="font-medium text-white font-mono">{{ ns.namespaceId }}</div>
                    <div class="text-xs text-slate-400">{{ ns.tenantId }}</div>
                  </td>
                  <td class="py-3 px-4">
                    <div class="font-medium text-slate-200">{{ ns.provider }}</div>
                    <div class="text-xs text-slate-400 font-mono">{{ ns.clusterName }}</div>
                  </td>
                  <td class="py-3 px-4 font-semibold text-white">
                    {{ ns.activePodsCount }} Pods
                  </td>
                  <td class="py-3 px-4 text-xs font-mono text-slate-300">
                    {{ ns.cpuQuotaCores }} Cores • {{ ns.memoryQuotaGb }} GB RAM
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-medium"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-300': ns.level === 'ZeroTrustMtlsEnforced',
                            'bg-rose-500/20 text-rose-300': ns.level === 'HardenedEbpfQuarantine',
                            'bg-slate-700 text-slate-300': ns.level === 'StandardNamespace'
                          }">
                      {{ ns.level }}
                    </span>
                  </td>
                  <td class="py-3 px-4">
                    @if (ns.isQuarantined) {
                      <span class="px-2 py-0.5 rounded text-xs font-bold bg-rose-500/20 text-rose-400 animate-pulse">
                        QUARANTINED
                      </span>
                    } @else {
                      <span class="px-2 py-0.5 rounded text-xs font-semibold bg-emerald-500/20 text-emerald-400">
                        HEALTHY
                      </span>
                    }
                  </td>
                  <td class="py-3 px-4 text-right">
                    @if (ns.isQuarantined) {
                      <button class="px-3 py-1 text-xs font-semibold rounded bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
                              (click)="toggleQuarantine(ns.namespaceId)"
                              [attr.data-testid]="'btn-toggle-' + ns.namespaceId">
                        Restore eBPF
                      </button>
                    } @else {
                      <button class="px-3 py-1 text-xs font-semibold rounded bg-rose-600 hover:bg-rose-500 text-white transition-colors"
                              (click)="toggleQuarantine(ns.namespaceId)"
                              [attr.data-testid]="'btn-toggle-' + ns.namespaceId">
                        Quarantine
                      </button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Action Toast -->
      @if (lastK8sNotice()) {
        <div class="p-3 bg-cyan-950/60 border border-cyan-500/40 rounded-xl text-cyan-300 text-xs flex items-center justify-between"
             data-testid="k8s-notice">
          <span>{{ lastK8sNotice() }}</span>
          <button (click)="lastK8sNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class K8sIsolationMeshCockpitComponent {
  readonly namespaces = signal<K8sTenantNamespace[]>([
    {
      namespaceId: 'ns-tenant-vertex',
      tenantId: 'tenant_vertex_enterprise',
      provider: 'AwsEks',
      clusterName: 'eks-us-east-prod-01',
      level: 'ZeroTrustMtlsEnforced',
      activePodsCount: 24,
      cpuQuotaCores: 64,
      memoryQuotaGb: 256,
      isQuarantined: false,
      createdAt: '2026-10-04T00:00:00Z'
    },
    {
      namespaceId: 'ns-tenant-apex',
      tenantId: 'tenant_apex_fintech',
      provider: 'GoogleGke',
      clusterName: 'gke-europe-west-prod-02',
      level: 'ZeroTrustMtlsEnforced',
      activePodsCount: 18,
      cpuQuotaCores: 48,
      memoryQuotaGb: 192,
      isQuarantined: false,
      createdAt: '2026-10-19T00:00:00Z'
    },
    {
      namespaceId: 'ns-tenant-sandbox-rogue',
      tenantId: 'tenant_unverified_trial',
      provider: 'AzureAks',
      clusterName: 'aks-us-west-stage-01',
      level: 'HardenedEbpfQuarantine',
      activePodsCount: 4,
      cpuQuotaCores: 8,
      memoryQuotaGb: 32,
      isQuarantined: true,
      createdAt: '2026-11-02T10:00:00Z'
    }
  ]);

  readonly lastK8sNotice = signal<string | null>(null);

  readonly quarantinedCount = computed(() =>
    this.namespaces().filter(n => n.isQuarantined).length
  );

  readonly totalPods = computed(() =>
    this.namespaces().reduce((sum, n) => sum + n.activePodsCount, 0)
  );

  toggleQuarantine(namespaceId: string): void {
    this.namespaces.update(list =>
      list.map(n => n.namespaceId === namespaceId ? {
        ...n,
        isQuarantined: !n.isQuarantined,
        level: !n.isQuarantined ? 'HardenedEbpfQuarantine' : 'ZeroTrustMtlsEnforced'
      } : n)
    );

    const updated = this.namespaces().find(n => n.namespaceId === namespaceId);
    this.lastK8sNotice.set(`Namespace ${namespaceId} is now ${updated?.isQuarantined ? 'Quarantined (eBPF egress blocked)' : 'Restored to ZeroTrust mTLS'}.`);
  }
}
