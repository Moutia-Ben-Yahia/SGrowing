import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface DbReplicaItem {
  replicaId: string;
  regionName: string;
  hostname: string;
  databaseEngine: string;
  replicationLagMs: number;
  rttLatencyMs: number;
  activeConnectionsCount: number;
  cpuUtilizationPct: number;
  nodeStatus: string;
}

@Component({
  selector: 'app-db-query-router-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="db-query-router-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-rose-500/20 text-rose-400 border border-rose-500/30">
              Horizon 115 • Domain K18 & A21
            </span>
            <span class="text-xs text-slate-400">Cross-Region DB Latency Probing &amp; Query Router</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">Database Read Replica Latency &amp; Query Router</h1>
          <p class="text-slate-400 text-sm mt-1">
            Active replication lag probing, SQL AST intent classification (Read/Write vs Read-Only), and circuit-breaker failover.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="probeReplicaLag()"
            data-testid="probe-lag-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Probe Replica Heartbeats
          </button>
          <button
            (click)="tripCircuitBreaker()"
            data-testid="trip-breaker-btn"
            class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-rose-600/30 flex items-center gap-2">
            Trip Replica Circuit Breaker
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Total Database Replicas</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-total-replicas">{{ totalReplicas() }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">Multi-Region PostgreSQL-17</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Healthy Active in Pool</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-healthy-nodes">{{ healthyNodes() }} / {{ totalReplicas() }}</p>
          <span class="text-xs text-slate-400 mt-1 block">Lag &lt; 100ms Threshold</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Read Traffic Offload Ratio</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-read-ratio">{{ readTrafficPct() }}%</p>
          <span class="text-xs text-indigo-300 mt-1 block">Primary CPU Shielded</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Routing Decision Latency</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-routing-speed">{{ routingDecisionUs() }} &mu;s</p>
          <span class="text-xs text-emerald-400 mt-1 block">In-Memory Sub-Millisecond</span>
        </div>
      </div>

      <!-- Replicas Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Multi-Region Database Nodes &amp; Replication Lag</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Node ID</th>
                <th class="py-3 px-4">Region</th>
                <th class="py-3 px-4">Internal Hostname</th>
                <th class="py-3 px-4 text-right">Replication Lag</th>
                <th class="py-3 px-4 text-right">RTT Latency</th>
                <th class="py-3 px-4 text-right">Active Conns</th>
                <th class="py-3 px-4 text-right">Node Health</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (rep of replicas(); track rep.replicaId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-indigo-300 font-semibold">{{ rep.replicaId }}</td>
                  <td class="py-3 px-4 text-white font-medium">{{ rep.regionName }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ rep.hostname }}</td>
                  <td class="py-3 px-4 text-right font-bold" [class.text-rose-400]="rep.replicationLagMs > 100" [class.text-emerald-400]="rep.replicationLagMs <= 100">
                    {{ rep.replicationLagMs }} ms
                  </td>
                  <td class="py-3 px-4 text-right text-slate-300">{{ rep.rttLatencyMs }} ms</td>
                  <td class="py-3 px-4 text-right font-semibold text-white">{{ rep.activeConnectionsCount }}</td>
                  <td class="py-3 px-4 text-right">
                    <span [class]="rep.nodeStatus === 'Healthy_InPool' ? 'px-2 py-0.5 text-xs rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium' : 'px-2 py-0.5 text-xs rounded-full bg-rose-500/20 text-rose-400 border border-rose-500/30 font-medium'">
                      {{ rep.nodeStatus }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class DbQueryRouterCockpitComponent {
  totalReplicas = signal<number>(4);
  healthyNodes = signal<number>(3);
  readTrafficPct = signal<number>(89.0);
  routingDecisionUs = signal<number>(45);

  replicas = signal<DbReplicaItem[]>([
    { replicaId: 'DB-PRIMARY-IAD', regionName: 'us-east-1', hostname: 'db-primary.iad.sgrowing.internal', databaseEngine: 'PostgreSQL-17', replicationLagMs: 0, rttLatencyMs: 4, activeConnectionsCount: 120, cpuUtilizationPct: 42.5, nodeStatus: 'Healthy_InPool' },
    { replicaId: 'DB-REPLICA-FRA', regionName: 'eu-central-1', hostname: 'db-rep.fra.sgrowing.internal', databaseEngine: 'PostgreSQL-17', replicationLagMs: 18, rttLatencyMs: 12, activeConnectionsCount: 65, cpuUtilizationPct: 31.0, nodeStatus: 'Healthy_InPool' },
    { replicaId: 'DB-REPLICA-SIN', regionName: 'ap-southeast-1', hostname: 'db-rep.sin.sgrowing.internal', databaseEngine: 'PostgreSQL-17', replicationLagMs: 45, rttLatencyMs: 22, activeConnectionsCount: 48, cpuUtilizationPct: 28.4, nodeStatus: 'Healthy_InPool' },
    { replicaId: 'DB-REPLICA-GRU', regionName: 'sa-east-1', hostname: 'db-rep.gru.sgrowing.internal', databaseEngine: 'PostgreSQL-17', replicationLagMs: 125, rttLatencyMs: 75, activeConnectionsCount: 20, cpuUtilizationPct: 68.2, nodeStatus: 'Lagging_Degraded' }
  ]);

  probeReplicaLag(): void {
    this.routingDecisionUs.set(38);
  }

  tripCircuitBreaker(): void {
    this.replicas.update(list =>
      list.map(r => r.replicaId === 'DB-REPLICA-GRU' ? { ...r, nodeStatus: 'Draining', activeConnectionsCount: 0 } : r)
    );
    this.healthyNodes.set(3);
  }
}
