import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type CustomerPltvTier =
  | 'ChampionWhale'
  | 'SteadyLoyal'
  | 'HighPotentialNewbie'
  | 'StandardOccasional'
  | 'AtRiskDefecting'
  | 'LostDormant';

export interface CustomerPltvRecord {
  id: string;
  customerName: string;
  email: string;
  frequency: number;
  recencyDays: number;
  totalObservationDays: number;
  historicalAov: number;
  totalLifetimeSpend: number;
  probabilityAlive: number;
  churnRiskScore: number;
  expectedTx365d: number;
  expectedAov: number;
  predictedResidualLtv365d: number;
  tier: CustomerPltvTier;
  requiresWinBack: boolean;
  status: 'Active' | 'InterventionQueued' | 'WonBack';
}

@Component({
  selector: 'app-pltv-analytics-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="text-3xl">📊</span>
            <div>
              <h1 class="text-2xl font-bold tracking-tight text-white">
                Predictive Customer Lifetime Value (pLTV)
              </h1>
              <p class="text-xs text-slate-400 mt-1">
                Probabilistic BG/NBD transaction model & Gamma-Gamma spend distribution radar.
              </p>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="triggerBulkWinBack()"
            [disabled]="atRiskCount() === 0"
            class="px-4 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white text-xs font-semibold shadow-lg shadow-rose-600/20 transition flex items-center gap-2">
            <span>⚡</span> Automated Win-Back Batch ({{ atRiskCount() }})
          </button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
          <div class="text-xs text-slate-400 font-medium">Champion Whales</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ championCount() }}</div>
          <div class="text-[11px] text-slate-500 mt-1">P(Alive) > 70% & pLTV > $2,000</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
          <div class="text-xs text-slate-400 font-medium">At-Risk Defecting VIPs</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ atRiskCount() }}</div>
          <div class="text-[11px] text-slate-500 mt-1">High past spend, decaying velocity</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
          <div class="text-xs text-slate-400 font-medium">Avg Retention Probability</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ averageProbabilityAlive() }}%</div>
          <div class="text-[11px] text-slate-500 mt-1">BG/NBD population mean</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
          <div class="text-xs text-slate-400 font-medium">Predicted Residual 365d LTV</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">\${{ totalPredicted365dLtv() | number:'1.0-0' }}</div>
          <div class="text-[11px] text-slate-500 mt-1">Expected forward cashflow</div>
        </div>
      </div>

      <!-- Interactive Single-Customer BG/NBD Simulator -->
      <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-5 space-y-4">
        <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
          <h2 class="text-sm font-semibold text-white flex items-center gap-2">
            <span>🧮</span> Interactive Latent Transaction Simulator
          </h2>
          <span class="text-xs text-slate-400">Tweak parameters to observe real-time Bayesian shrinkage</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div>
            <label class="block text-xs text-slate-400 mb-1">Repeat Frequency (x)</label>
            <input
              type="number"
              [(ngModel)]="simFrequency"
              min="0"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white" />
          </div>

          <div>
            <label class="block text-xs text-slate-400 mb-1">Recency Days (tx)</label>
            <input
              type="number"
              [(ngModel)]="simRecency"
              min="0"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white" />
          </div>

          <div>
            <label class="block text-xs text-slate-400 mb-1">Observation Days (T)</label>
            <input
              type="number"
              [(ngModel)]="simTotalDays"
              min="1"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white" />
          </div>

          <div>
            <label class="block text-xs text-slate-400 mb-1">Avg Order Value ($)</label>
            <input
              type="number"
              [(ngModel)]="simAov"
              min="1"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white" />
          </div>
        </div>

        <div class="p-3 bg-slate-950/80 border border-cyan-500/30 rounded-lg flex flex-wrap items-center justify-between gap-4">
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400">P(Alive):</span>
            <span class="text-xs font-bold text-cyan-400">{{ simPAlive() }}%</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400">Churn Risk:</span>
            <span class="text-xs font-bold text-rose-400">{{ simChurnRisk() }}%</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400">Expected 365d Tx:</span>
            <span class="text-xs font-bold text-amber-400">{{ simExpectedTx() }}</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400">Expected AOV:</span>
            <span class="text-xs font-bold text-emerald-400">\${{ simExpectedAov() | number:'1.2-2' }}</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400">365d pLTV:</span>
            <span class="text-xs font-bold text-white">\${{ simPltv365d() | number:'1.2-2' }}</span>
          </div>
        </div>
      </div>

      <!-- Cohort Customers Ledger -->
      <div class="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden">
        <div class="p-4 border-b border-slate-800 flex items-center justify-between">
          <h2 class="text-sm font-semibold text-white">Cohort Analysis & Action Ledger</h2>
          <span class="text-xs text-slate-400">{{ customers().length }} accounts indexed</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-xs">
            <thead class="bg-slate-950/60 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
              <tr>
                <th class="p-3">Customer</th>
                <th class="p-3">Frequency / Recency</th>
                <th class="p-3">Historical Spend</th>
                <th class="p-3">P(Alive) & Churn Risk</th>
                <th class="p-3">Forward 365d pLTV</th>
                <th class="p-3">Tier</th>
                <th class="p-3 text-right">Intervention</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800/60">
              @for (cust of customers(); track cust.id) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="p-3">
                    <div class="font-medium text-white">{{ cust.customerName }}</div>
                    <div class="text-[11px] text-slate-400">{{ cust.email }}</div>
                  </td>
                  <td class="p-3 text-slate-300">
                    <div>x = {{ cust.frequency }} orders</div>
                    <div class="text-[11px] text-slate-500">{{ cust.recencyDays }}d / {{ cust.totalObservationDays }}d</div>
                  </td>
                  <td class="p-3 font-mono text-white">
                    \${{ cust.totalLifetimeSpend | number:'1.2-2' }}
                  </td>
                  <td class="p-3">
                    <div class="flex items-center gap-2">
                      <div class="w-16 bg-slate-800 rounded-full h-1.5 overflow-hidden">
                        <div
                          class="h-full rounded-full"
                          [style.width.%]="cust.probabilityAlive * 100"
                          [class]="cust.probabilityAlive > 0.6 ? 'bg-emerald-500' : cust.probabilityAlive > 0.35 ? 'bg-amber-500' : 'bg-rose-500'"></div>
                      </div>
                      <span class="font-mono text-[11px]">{{ (cust.probabilityAlive * 100) | number:'1.0-0' }}%</span>
                    </div>
                    <div class="text-[10px] text-rose-400 mt-0.5">Risk: {{ cust.churnRiskScore }}%</div>
                  </td>
                  <td class="p-3 font-mono text-amber-300 font-semibold">
                    \${{ cust.predictedResidualLtv365d | number:'1.2-2' }}
                  </td>
                  <td class="p-3">
                    <span
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                      [ngClass]="getTierClass(cust.tier)">
                      {{ cust.tier }}
                    </span>
                  </td>
                  <td class="p-3 text-right">
                    @if (cust.requiresWinBack && cust.status === 'Active') {
                      <button
                        (click)="queueIntervention(cust)"
                        class="px-2.5 py-1 rounded bg-rose-600/30 hover:bg-rose-600 border border-rose-500/50 text-rose-200 text-[11px] transition">
                        Dispatch Win-Back
                      </button>
                    } @else if (cust.status === 'InterventionQueued') {
                      <span class="text-xs text-amber-400 font-medium">Queued</span>
                    } @else {
                      <span class="text-xs text-slate-500">—</span>
                    }
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
export class PltvAnalyticsStudioComponent {
  simFrequency = 4;
  simRecency = 120;
  simTotalDays = 180;
  simAov = 150;

  customers = signal<CustomerPltvRecord[]>([
    {
      id: 'cust-1',
      customerName: 'Enterprise Logistics Corp',
      email: 'procurement@elc.com',
      frequency: 14,
      recencyDays: 320,
      totalObservationDays: 340,
      historicalAov: 280,
      totalLifetimeSpend: 3920,
      probabilityAlive: 0.88,
      churnRiskScore: 12.0,
      expectedTx365d: 14.8,
      expectedAov: 242.1,
      predictedResidualLtv365d: 3583.08,
      tier: 'ChampionWhale',
      requiresWinBack: false,
      status: 'Active'
    },
    {
      id: 'cust-2',
      customerName: 'Apex Health Systems',
      email: 'supplies@apexhealth.org',
      frequency: 9,
      recencyDays: 110,
      totalObservationDays: 360,
      historicalAov: 210,
      totalLifetimeSpend: 1890,
      probabilityAlive: 0.32,
      churnRiskScore: 68.0,
      expectedTx365d: 2.8,
      expectedAov: 152.8,
      predictedResidualLtv365d: 427.84,
      tier: 'AtRiskDefecting',
      requiresWinBack: true,
      status: 'Active'
    },
    {
      id: 'cust-3',
      customerName: 'Nordic Clean Solutions',
      email: 'jens@nordicclean.se',
      frequency: 1,
      recencyDays: 14,
      totalObservationDays: 20,
      historicalAov: 420,
      totalLifetimeSpend: 420,
      probabilityAlive: 0.85,
      churnRiskScore: 15.0,
      expectedTx365d: 12.2,
      expectedAov: 111.6,
      predictedResidualLtv365d: 1361.52,
      tier: 'HighPotentialNewbie',
      requiresWinBack: false,
      status: 'Active'
    },
    {
      id: 'cust-4',
      customerName: 'Vintage Retail Outlet',
      email: 'orders@vintageretail.com',
      frequency: 6,
      recencyDays: 210,
      totalObservationDays: 250,
      historicalAov: 130,
      totalLifetimeSpend: 780,
      probabilityAlive: 0.62,
      churnRiskScore: 38.0,
      expectedTx365d: 5.6,
      expectedAov: 93.6,
      predictedResidualLtv365d: 524.16,
      tier: 'SteadyLoyal',
      requiresWinBack: false,
      status: 'Active'
    }
  ]);

  championCount = computed(() => this.customers().filter(c => c.tier === 'ChampionWhale').length);
  atRiskCount = computed(() => this.customers().filter(c => c.tier === 'AtRiskDefecting').length);
  averageProbabilityAlive = computed(() => {
    const list = this.customers();
    if (list.length === 0) return 0;
    const avg = list.reduce((sum, c) => sum + c.probabilityAlive, 0) / list.length;
    return Math.round(avg * 100);
  });
  totalPredicted365dLtv = computed(() => {
    return this.customers().reduce((sum, c) => sum + c.predictedResidualLtv365d, 0);
  });

  simPAlive = computed(() => {
    const T = Math.max(1, this.simTotalDays);
    const tx = Math.min(T, Math.max(0, this.simRecency));
    const x = Math.max(0, this.simFrequency);
    const expectedInter = T / (x + 1);
    const recencyGap = Math.max(0, T - tx);
    const decay = recencyGap / Math.max(1, expectedInter);
    const p = 1.0 / (1.0 + Math.pow(decay, 1.25));
    return Math.round(p * 100);
  });

  simChurnRisk = computed(() => 100 - this.simPAlive());

  simExpectedTx = computed(() => {
    const p = this.simPAlive() / 100.0;
    const T = Math.max(1, this.simTotalDays);
    const x = Math.max(0, this.simFrequency);
    const velocity = (x + 1) / (T + 30);
    return Math.round(p * velocity * 365 * 10) / 10;
  });

  simExpectedAov = computed(() => {
    const x = Math.max(0, this.simFrequency);
    const mx = Math.max(0, this.simAov);
    return ((x * mx) + (5 * 50.0)) / (x + 5);
  });

  simPltv365d = computed(() => {
    return Math.round(this.simExpectedTx() * this.simExpectedAov() * 100) / 100;
  });

  getTierClass(tier: CustomerPltvTier): string {
    switch (tier) {
      case 'ChampionWhale':
        return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30';
      case 'SteadyLoyal':
        return 'bg-cyan-500/20 text-cyan-300 border-cyan-500/30';
      case 'HighPotentialNewbie':
        return 'bg-purple-500/20 text-purple-300 border-purple-500/30';
      case 'AtRiskDefecting':
        return 'bg-rose-500/20 text-rose-300 border-rose-500/30';
      case 'LostDormant':
        return 'bg-slate-700/40 text-slate-400 border-slate-600/30';
      default:
        return 'bg-blue-500/20 text-blue-300 border-blue-500/30';
    }
  }

  queueIntervention(cust: CustomerPltvRecord) {
    this.customers.update(items =>
      items.map(i => (i.id === cust.id ? { ...i, status: 'InterventionQueued' } : i))
    );
  }

  triggerBulkWinBack() {
    this.customers.update(items =>
      items.map(i => (i.requiresWinBack ? { ...i, status: 'InterventionQueued' } : i))
    );
  }
}
