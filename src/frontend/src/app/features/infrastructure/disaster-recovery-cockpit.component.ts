import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ClusterNodeView {
  nodeId: string;
  provider: string;
  region: string;
  role: 'PrimaryActive' | 'HotStandbySynchronous' | 'WitnessQuorumNode';
  health: 'HealthySynchronized' | 'DegradedReplicationLag' | 'UnreachableOutage';
  replicationLagMs: number;
}

@Component({
  selector: 'app-disaster-recovery-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="disaster-recovery-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-teal-400">
            <span class="inline-block w-2 h-2 rounded-full bg-teal-400 animate-pulse"></span>
            Zero-Trust High Availability & Resilience • Horizon 70
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Multi-Cloud Disaster Recovery & Hot-Standby Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Sub-second RPO cross-cloud replication, Raft quorum consensus & automated sub-30s Anycast BGP failover.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="executeFailoverToGcp()"
            data-testid="promote-standby-btn"
            class="px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-teal-600/20"
          >
            <span>🔄</span> Promote GCP Europe Standby
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Primary Cluster</div>
          <div class="text-2xl font-bold text-teal-400 mt-1 truncate" data-testid="stat-primary-node">
            {{ activePrimary() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Global Anycast Ingress Active</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Quorum Consensus</div>
          <div class="text-2xl font-bold mt-1" [class]="quorumHealthy() ? 'text-emerald-400' : 'text-rose-400'" data-testid="stat-quorum">
            {{ quorumHealthy() ? 'ESTABLISHED (4/4)' : 'QUORUM COMPROMISED' }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Raft Anti-Split-Brain Protection</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Recovery Point Objective (RPO)</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-rpo">
            0.12 s
          </div>
          <div class="text-emerald-400 text-xs mt-1">Synchronous Multi-Cloud WAL</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Recovery Time Benchmark (RTO)</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-rto">
            14.8 s
          </div>
          <div class="text-teal-400 text-xs mt-1">Automated Zero-Downtime DNS Cutover</div>
        </div>
      </div>

      <!-- Topology Grid -->
      <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
        <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
          <h2 class="text-lg font-bold text-white">Cross-Cloud Heterogeneous Cluster Topology</h2>
          <span class="text-xs text-slate-400 font-mono">AWS • GCP • Azure • Sovereign VPC</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 p-5">
          @for (node of nodes(); track node.nodeId) {
            <div
              class="p-4 rounded-xl border space-y-3 transition-colors"
              [class]="node.role === 'PrimaryActive' ? 'bg-teal-950/40 border-teal-600/60 shadow-lg shadow-teal-950/30' : 'bg-slate-900 border-slate-800'"
              [attr.data-testid]="'node-card-' + node.nodeId"
            >
              <div class="flex items-center justify-between">
                <span class="text-xs font-mono font-bold text-slate-400">{{ node.region }}</span>
                <span
                  class="px-2 py-0.5 rounded text-[10px] font-semibold"
                  [class]="node.role === 'PrimaryActive' ? 'bg-teal-900/80 text-teal-300 border border-teal-700' : 'bg-slate-800 text-slate-300'"
                >
                  {{ node.role }}
                </span>
              </div>

              <div>
                <h3 class="font-bold text-white text-base">{{ node.provider }}</h3>
                <p class="text-xs text-slate-400 mt-0.5 font-mono">{{ node.nodeId }}</p>
              </div>

              <div class="pt-2 border-t border-slate-800 flex items-center justify-between text-xs">
                <span class="text-slate-400">Replication Lag:</span>
                <span class="font-mono font-bold" [class]="node.replicationLagMs < 25 ? 'text-emerald-400' : 'text-amber-400'">
                  {{ node.replicationLagMs }} ms
                </span>
              </div>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class DisasterRecoveryCockpitComponent {
  readonly activePrimary = signal('AWS us-east-1');
  readonly quorumHealthy = signal(true);

  readonly nodes = signal<ClusterNodeView[]>([
    { nodeId: 'node_aws_primary', provider: 'AWS US-East', region: 'us-east-1', role: 'PrimaryActive', health: 'HealthySynchronized', replicationLagMs: 0.0 },
    { nodeId: 'node_gcp_standby', provider: 'GCP Europe West', region: 'europe-west3', role: 'HotStandbySynchronous', health: 'HealthySynchronized', replicationLagMs: 12.4 },
    { nodeId: 'node_azure_standby', provider: 'Azure US East', region: 'eastus2', role: 'HotStandbySynchronous', health: 'HealthySynchronized', replicationLagMs: 18.2 },
    { nodeId: 'node_sovereign_witness', provider: 'Sovereign VPC', region: 'eu-central-de', role: 'WitnessQuorumNode', health: 'HealthySynchronized', replicationLagMs: 8.5 }
  ]);

  executeFailoverToGcp(): void {
    this.activePrimary.set('GCP Europe West');
    this.nodes.set([
      { nodeId: 'node_aws_primary', provider: 'AWS US-East', region: 'us-east-1', role: 'HotStandbySynchronous', health: 'HealthySynchronized', replicationLagMs: 15.1 },
      { nodeId: 'node_gcp_standby', provider: 'GCP Europe West', region: 'europe-west3', role: 'PrimaryActive', health: 'HealthySynchronized', replicationLagMs: 0.0 },
      { nodeId: 'node_azure_standby', provider: 'Azure US East', region: 'eastus2', role: 'HotStandbySynchronous', health: 'HealthySynchronized', replicationLagMs: 18.2 },
      { nodeId: 'node_sovereign_witness', provider: 'Sovereign VPC', region: 'eu-central-de', role: 'WitnessQuorumNode', health: 'HealthySynchronized', replicationLagMs: 8.5 }
    ]);
  }
}
