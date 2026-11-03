import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface UsageMetric {
  metricName: string;
  consumedUnits: number;
  quotaLimit: number;
  unitCostUsd: number;
  unitLabel: string;
}

export interface TenantConsumptionProfile {
  tenantId: string;
  tenantName: string;
  tier: 'Startup' | 'Growth' | 'EnterpriseScale';
  baseFeeMonthlyUsd: number;
  metrics: UsageMetric[];
  autoUpgradeEnabled: boolean;
}

@Component({
  selector: 'app-consumption-metering-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="consumption-metering-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-amber-500/20 text-amber-400 border border-amber-500/30">Horizon 79</span>
            <span class="text-xs text-slate-400">SaaS Usage-Based Billing</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Consumption Metering & Quota Escalator</h1>
          <p class="text-sm text-slate-400">Real-time event stream ingestion, dynamic burn-rate monitoring, and threshold escalation.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="ingestSimulatedTraffic()" data-testid="ingest-traffic-btn" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded font-medium text-sm transition-colors">
            Simulate Traffic Spike
          </button>
          <button (click)="autoUpgradeCurrentTenant()" data-testid="upgrade-tier-btn" class="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded font-medium text-sm transition-colors shadow">
            Upgrade Tier & Expand Quota
          </button>
        </div>
      </header>

      <!-- KPI Summary Cards -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-current-tier">
          <div class="text-xs font-medium text-slate-400 uppercase">Subscription Tier</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ profile().tier }}</div>
          <div class="text-xs text-slate-400 mt-1">Base Plan: \${{ profile().baseFeeMonthlyUsd }}/mo</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-burn-rate">
          <div class="text-xs font-medium text-slate-400 uppercase">Max Quota Burn Rate</div>
          <div class="text-2xl font-bold" [ngClass]="highestBurnRate() > 85 ? 'text-rose-400' : 'text-emerald-400'" mt-1>
            {{ highestBurnRate() }}%
          </div>
          <div class="text-xs text-slate-400 mt-1">{{ highestBurnRate() > 80 ? 'Threshold Alert Active' : 'Normal Consumption' }}</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-projected-invoice">
          <div class="text-xs font-medium text-slate-400 uppercase">Projected Month-End Invoice</div>
          <div class="text-2xl font-bold text-white mt-1">\${{ projectedInvoiceTotal() | number:'1.2-2' }}</div>
          <div class="text-xs text-slate-400 mt-1">Base \${{ profile().baseFeeMonthlyUsd }} + \${{ projectedOverages() | number:'1.2-2' }} overage</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-auto-upgrade">
          <div class="text-xs font-medium text-slate-400 uppercase">Autonomous Quota Escalation</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">Active</div>
          <div class="text-xs text-slate-400 mt-1">Zero 429 rate limit drops</div>
        </div>
      </section>

      <!-- Active Meters -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5 mb-6">
        <h2 class="text-base font-semibold text-white mb-4">Active Usage Meters & Quota Boundaries</h2>
        <div class="space-y-4">
          @for (metric of profile().metrics; track metric.metricName) {
            <div class="bg-slate-900/60 border border-slate-700/50 p-4 rounded-lg" [attr.data-testid]="'metric-card-' + metric.metricName.toLowerCase().replace(' ', '-')">
              <div class="flex flex-col md:flex-row md:items-center justify-between gap-2 mb-2">
                <div>
                  <span class="text-sm font-semibold text-white">{{ metric.metricName }}</span>
                  <span class="text-xs text-slate-400 ml-2">(\${{ metric.unitCostUsd }} per {{ metric.unitLabel }})</span>
                </div>
                <div class="text-xs text-slate-300">
                  <span class="font-bold text-white">{{ metric.consumedUnits | number }}</span> / {{ metric.quotaLimit | number }} {{ metric.unitLabel }}s
                  ({{ getBurnPercent(metric) }}%)
                </div>
              </div>

              <!-- Progress bar -->
              <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                <div class="h-full rounded-full transition-all duration-300"
                  [ngClass]="{
                    'bg-emerald-500': getBurnPercent(metric) < 70,
                    'bg-amber-500': getBurnPercent(metric) >= 70 && getBurnPercent(metric) < 90,
                    'bg-rose-500': getBurnPercent(metric) >= 90
                  }"
                  [style.width.%]="Math.min(100, getBurnPercent(metric))">
                </div>
              </div>

              @if (getBurnPercent(metric) > 85) {
                <div class="mt-2 text-xs text-rose-400 flex items-center gap-1.5" data-testid="burn-rate-warning">
                  <span>Warning: Quota consumption reached {{ getBurnPercent(metric) }}%. Overages billed at standard per-unit rate.</span>
                </div>
              }
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class ConsumptionMeteringCockpitComponent {
  Math = Math;

  profile = signal<TenantConsumptionProfile>({
    tenantId: 'tenant_acme_corp',
    tenantName: 'Acme Global Enterprise',
    tier: 'Growth',
    baseFeeMonthlyUsd: 499,
    autoUpgradeEnabled: true,
    metrics: [
      {
        metricName: 'API Invocations',
        consumedUnits: 480000,
        quotaLimit: 1000000,
        unitCostUsd: 0.0005,
        unitLabel: 'request'
      },
      {
        metricName: 'Vector Embedding Tokens',
        consumedUnits: 12000000,
        quotaLimit: 20000000,
        unitCostUsd: 0.00002,
        unitLabel: 'token'
      },
      {
        metricName: 'GPU Compute Seconds',
        consumedUnits: 3500,
        quotaLimit: 5000,
        unitCostUsd: 0.05,
        unitLabel: 'second'
      }
    ]
  });

  getBurnPercent(metric: UsageMetric): number {
    if (metric.quotaLimit === 0) return 0;
    return Math.round((metric.consumedUnits / metric.quotaLimit) * 100);
  }

  highestBurnRate = computed(() => {
    const list = this.profile().metrics;
    if (list.length === 0) return 0;
    return Math.max(...list.map(m => this.getBurnPercent(m)));
  });

  projectedOverages = computed(() => {
    let overageCost = 0;
    for (const m of this.profile().metrics) {
      if (m.consumedUnits > m.quotaLimit) {
        overageCost += (m.consumedUnits - m.quotaLimit) * m.unitCostUsd;
      }
    }
    return overageCost;
  });

  projectedInvoiceTotal = computed(() => {
    return this.profile().baseFeeMonthlyUsd + this.projectedOverages();
  });

  ingestSimulatedTraffic() {
    this.profile.update(p => ({
      ...p,
      metrics: p.metrics.map(m => {
        if (m.metricName === 'GPU Compute Seconds') {
          return { ...m, consumedUnits: m.consumedUnits + 2000 }; // 3500 + 2000 = 5500, breaches 5000
        }
        return m;
      })
    }));
  }

  autoUpgradeCurrentTenant() {
    this.profile.update(p => ({
      ...p,
      tier: 'EnterpriseScale',
      baseFeeMonthlyUsd: 999,
      metrics: p.metrics.map(m => ({
        ...m,
        quotaLimit: m.quotaLimit * 2
      }))
    }));
  }
}
