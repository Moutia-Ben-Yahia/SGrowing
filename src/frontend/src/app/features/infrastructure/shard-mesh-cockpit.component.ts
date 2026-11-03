import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ShardNode {
  shardId: string;
  region: string;
  hostEndpoint: string;
  storageUtilizationPercentage: number;
  queriesPerSecondIops: number;
  assignedVirtualBuckets: number;
  isRebalancing: boolean;
}

export interface TenantRoutingResult {
  tenantId: string;
  assignedShardId: string;
  virtualHashBucket: number;
  regionEndpoint: string;
}

@Component({
  selector: 'app-shard-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="shard-mesh-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">Horizon 80</span>
            <span class="text-xs text-slate-400">Database Distributed Infrastructure</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Multi-Region Active-Active Database Shard Mesh</h1>
          <p class="text-sm text-slate-400">Virtual bucket hashing, continuous multi-region load rebalancing, and zero-downtime tenant routing.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="rebalanceMesh()" data-testid="rebalance-mesh-btn" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-medium text-sm transition-colors shadow">
            Auto-Rebalance Shard Mesh
          </button>
        </div>
      </header>

      <!-- KPI Cluster Status -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-shards">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Shards</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ shards().length }} Nodes</div>
          <div class="text-xs text-slate-400 mt-1">1024 Virtual Buckets Allocated</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-cluster-balance">
          <div class="text-xs font-medium text-slate-400 uppercase">Cluster Balance State</div>
          <div class="text-2xl font-bold" [ngClass]="isMeshBalanced() ? 'text-emerald-400' : 'text-amber-400'" mt-1>
            {{ isMeshBalanced() ? 'Balanced' : 'Imbalanced' }}
          </div>
          <div class="text-xs text-slate-400 mt-1">Variance: {{ maxStorageVariance() }}% (Limit: 20%)</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-throughput">
          <div class="text-xs font-medium text-slate-400 uppercase">Aggregate Cluster Throughput</div>
          <div class="text-2xl font-bold text-white mt-1">{{ totalQps() | number }} IOPS</div>
          <div class="text-xs text-slate-400 mt-1">Real-time cross-region queries</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-storage">
          <div class="text-xs font-medium text-slate-400 uppercase">Distributed Cluster Storage</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">48.5 TB</div>
          <div class="text-xs text-slate-400 mt-1">Global active-active replication</div>
        </div>
      </section>

      <!-- Shard Nodes Grid -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        @for (shard of shards(); track shard.shardId) {
          <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5" [attr.data-testid]="'shard-card-' + shard.shardId">
            <div class="flex items-center justify-between mb-3">
              <span class="text-sm font-bold text-white">{{ shard.shardId }}</span>
              <span class="px-2 py-0.5 text-xs font-medium rounded bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                {{ shard.region }}
              </span>
            </div>

            <div class="text-xs text-slate-400 mb-3 font-mono">{{ shard.hostEndpoint }}</div>

            <div class="space-y-3">
              <div>
                <div class="flex justify-between text-xs mb-1">
                  <span class="text-slate-400">Storage Utilization</span>
                  <span class="font-bold text-white">{{ shard.storageUtilizationPercentage }}%</span>
                </div>
                <div class="w-full bg-slate-700 rounded-full h-2 overflow-hidden">
                  <div class="h-full rounded-full transition-all duration-300"
                    [ngClass]="shard.storageUtilizationPercentage > 80 ? 'bg-amber-500' : 'bg-emerald-500'"
                    [style.width.%]="shard.storageUtilizationPercentage">
                  </div>
                </div>
              </div>

              <div class="grid grid-cols-2 gap-2 text-xs pt-2 border-t border-slate-700/40">
                <div>
                  <span class="text-slate-400 block">Virtual Buckets</span>
                  <span class="font-bold text-white text-sm">{{ shard.assignedVirtualBuckets }}</span>
                </div>
                <div>
                  <span class="text-slate-400 block">QPS Load</span>
                  <span class="font-bold text-white text-sm">{{ shard.queriesPerSecondIops | number }}</span>
                </div>
              </div>
            </div>
          </div>
        }
      </div>

      <!-- Tenant Routing Tester -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
        <h2 class="text-base font-semibold text-white mb-2">Deterministic Tenant Hash Shard Router</h2>
        <p class="text-xs text-slate-400 mb-4">Calculate deterministic SHA-256 partition bucket and regional host endpoint for any tenant ID.</p>

        <div class="flex flex-col sm:flex-row items-center gap-3">
          <input type="text" [(ngModel)]="testTenantId" data-testid="tenant-id-input" placeholder="Enter Tenant Identifier (e.g. tenant_nordic_fintech)" class="w-full sm:w-96 px-3 py-2 bg-slate-900 border border-slate-700 rounded text-sm text-slate-100 focus:outline-none focus:border-emerald-500" />
          <button (click)="routeTestTenant()" data-testid="test-route-btn" class="w-full sm:w-auto px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded text-sm font-medium transition-colors">
            Resolve Route
          </button>
        </div>

        @if (routingResult(); as res) {
          <div class="mt-4 p-4 bg-slate-900/80 border border-slate-700/60 rounded-lg" data-testid="routing-result-panel">
            <div class="text-xs text-slate-400 uppercase font-semibold mb-2">Partitioning Resolution</div>
            <div class="grid grid-cols-1 sm:grid-cols-4 gap-4 text-xs">
              <div>
                <span class="text-slate-500 block">Tenant ID:</span>
                <span class="text-slate-200 font-medium">{{ res.tenantId }}</span>
              </div>
              <div>
                <span class="text-slate-500 block">Assigned Shard:</span>
                <span class="text-emerald-400 font-bold" data-testid="assigned-shard-id">{{ res.assignedShardId }}</span>
              </div>
              <div>
                <span class="text-slate-500 block">Hash Bucket:</span>
                <span class="text-white font-mono" data-testid="assigned-bucket">Bucket #{{ res.virtualHashBucket }} / 1024</span>
              </div>
              <div>
                <span class="text-slate-500 block">Host Endpoint:</span>
                <span class="text-slate-300 font-mono">{{ res.regionEndpoint }}</span>
              </div>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class ShardMeshCockpitComponent {
  testTenantId = 'tenant_aurora_pay_01';

  shards = signal<ShardNode[]>([
    {
      shardId: 'shard_us_east',
      region: 'UsEastPrimary',
      hostEndpoint: 'pg-shard-us.internal:5432',
      storageUtilizationPercentage: 84.5,
      queriesPerSecondIops: 4200,
      assignedVirtualBuckets: 342,
      isRebalancing: false
    },
    {
      shardId: 'shard_eu_west',
      region: 'EuropeWestPrimary',
      hostEndpoint: 'pg-shard-eu.internal:5432',
      storageUtilizationPercentage: 52.0,
      queriesPerSecondIops: 2800,
      assignedVirtualBuckets: 341,
      isRebalancing: false
    },
    {
      shardId: 'shard_apac_south',
      region: 'ApacSouthPrimary',
      hostEndpoint: 'pg-shard-apac.internal:5432',
      storageUtilizationPercentage: 48.0,
      queriesPerSecondIops: 2100,
      assignedVirtualBuckets: 341,
      isRebalancing: false
    }
  ]);

  routingResult = signal<TenantRoutingResult | null>({
    tenantId: 'tenant_aurora_pay_01',
    assignedShardId: 'shard_us_east',
    virtualHashBucket: 142,
    regionEndpoint: 'pg-shard-us.internal:5432'
  });

  totalQps = computed(() => this.shards().reduce((sum, s) => sum + s.queriesPerSecondIops, 0));

  maxStorageVariance = computed(() => {
    const list = this.shards();
    if (list.length === 0) return 0;
    const max = Math.max(...list.map(s => s.storageUtilizationPercentage));
    const min = Math.min(...list.map(s => s.storageUtilizationPercentage));
    return Math.round((max - min) * 10) / 10;
  });

  isMeshBalanced = computed(() => this.maxStorageVariance() < 20.0);

  routeTestTenant() {
    // Deterministic mock routing based on string length & char codes
    const tid = this.testTenantId || 'tenant_default';
    const sum = tid.split('').reduce((acc, c) => acc + c.charCodeAt(0), 0);
    const bucket = sum % 1024;
    const shardIdx = bucket % this.shards().length;
    const chosen = this.shards()[shardIdx];

    this.routingResult.set({
      tenantId: tid,
      assignedShardId: chosen.shardId,
      virtualHashBucket: bucket,
      regionEndpoint: chosen.hostEndpoint
    });
  }

  rebalanceMesh() {
    this.shards.update(list => list.map(s => {
      if (s.shardId === 'shard_us_east') {
        return {
          ...s,
          assignedVirtualBuckets: s.assignedVirtualBuckets - 30,
          storageUtilizationPercentage: 65.5
        };
      }
      if (s.shardId === 'shard_eu_west') {
        return {
          ...s,
          assignedVirtualBuckets: s.assignedVirtualBuckets + 15,
          storageUtilizationPercentage: 58.5
        };
      }
      if (s.shardId === 'shard_apac_south') {
        return {
          ...s,
          assignedVirtualBuckets: s.assignedVirtualBuckets + 15,
          storageUtilizationPercentage: 55.5
        };
      }
      return s;
    }));
  }
}
