import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface TerritoryWorkload {
  territoryId: string;
  territoryName: string;
  assignedRepId: string;
  repName: string;
  accountCount: number;
  totalPipelinePotential: number;
  workloadIndex: number;
  quotaAttainmentPct: number;
}

export interface RebalanceMove {
  accountId: string;
  accountName: string;
  fromRepName: string;
  toRepName: string;
  rationale: string;
  expectedWorkloadDeltaPct: number;
}

@Component({
  selector: 'app-territory-rebalancing-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="territory-rebalancing-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              Horizon 106 • Domain B18 & J16
            </span>
            <span class="text-xs text-slate-400">Autonomous Quota & Potential Rebalancing</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">Dynamic Sales Territory Rebalancing</h1>
          <p class="text-slate-400 text-sm mt-1">
            Workload equilibrium optimization, Gini coefficient reduction, and account migration autopilot.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="simulateEquilibrium()"
            data-testid="simulate-rebalance-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Simulate Equilibrium Plan
          </button>
          <button
            (click)="applyRebalance()"
            data-testid="apply-rebalance-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-emerald-600/30 flex items-center gap-2">
            Apply Scheduled Plan
          </button>
        </div>
      </div>

      <!-- Metric Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Total Accounts Covered</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-total-accounts">{{ totalAccounts() }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">4 Core Sales Territories</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Workload Gini Index</span>
          <p class="text-2xl font-bold text-amber-400 mt-1" data-testid="metric-gini-index">{{ giniIndex() }}</p>
          <span class="text-xs text-slate-400 mt-1 block">Optimal Threshold &lt; 0.20</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Efficiency Gain Potential</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-efficiency-gain">+{{ efficiencyGainPct() }}%</p>
          <span class="text-xs text-indigo-300 mt-1 block">Reduced Travel &amp; Overload</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Scheduled Rebalance Status</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-plan-status">{{ planStatus() }}</p>
          <span class="text-xs text-slate-400 mt-1 block">Grace Period: 14 Days</span>
        </div>
      </div>

      <!-- Territories Grid -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Territory Workload Distribution</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          @for (terr of territories(); track terr.territoryId) {
            <div class="bg-slate-800/60 border border-slate-700/60 p-4 rounded-xl flex flex-col justify-between">
              <div>
                <div class="flex justify-between items-start mb-2">
                  <span class="text-sm font-semibold text-white">{{ terr.territoryName }}</span>
                  <span
                    [class]="terr.workloadIndex > 1.3 ? 'px-2 py-0.5 text-xs rounded bg-rose-500/20 text-rose-300' : 'px-2 py-0.5 text-xs rounded bg-emerald-500/20 text-emerald-300'">
                    Index: {{ terr.workloadIndex }}
                  </span>
                </div>
                <p class="text-xs text-slate-400">Assigned Rep: <span class="text-slate-200 font-medium">{{ terr.repName }}</span></p>
                <p class="text-xs text-slate-400 mt-1">Accounts: <span class="text-white font-semibold">{{ terr.accountCount }}</span></p>
                <p class="text-xs text-slate-400">Pipeline: <span class="text-indigo-300 font-semibold">\${{ terr.totalPipelinePotential | number }}</span></p>
              </div>
              <div class="mt-4 pt-3 border-t border-slate-700/50 flex justify-between items-center text-xs">
                <span class="text-slate-400">Quota Attainment:</span>
                <span class="font-bold text-white">{{ terr.quotaAttainmentPct }}%</span>
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Planned Moves Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Algorithmic Rebalancing Recommendations</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Account</th>
                <th class="py-3 px-4">Current Owner</th>
                <th class="py-3 px-4">Proposed Owner</th>
                <th class="py-3 px-4">Strategic Rationale</th>
                <th class="py-3 px-4 text-right">Workload Delta</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (move of recommendations(); track move.accountId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-medium text-white">{{ move.accountName }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ move.fromRepName }}</td>
                  <td class="py-3 px-4 text-indigo-300 font-medium">{{ move.toRepName }}</td>
                  <td class="py-3 px-4 text-xs text-slate-400">{{ move.rationale }}</td>
                  <td class="py-3 px-4 text-right font-semibold text-emerald-400">{{ move.expectedWorkloadDeltaPct }}%</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class TerritoryRebalancingCockpitComponent {
  totalAccounts = signal<number>(156);
  giniIndex = signal<number>(0.38);
  efficiencyGainPct = signal<number>(24.6);
  planStatus = signal<string>('Ready for Simulation');

  territories = signal<TerritoryWorkload[]>([
    { territoryId: 'TERR-NA-EAST', territoryName: 'North America East', assignedRepId: 'REP-101', repName: 'Sarah Jenkins', accountCount: 48, totalPipelinePotential: 4850000, workloadIndex: 1.42, quotaAttainmentPct: 118.5 },
    { territoryId: 'TERR-NA-WEST', territoryName: 'North America West', assignedRepId: 'REP-102', repName: 'Michael Chang', accountCount: 32, totalPipelinePotential: 2900000, workloadIndex: 0.88, quotaAttainmentPct: 89.2 },
    { territoryId: 'TERR-EMEA-CENTRAL', territoryName: 'EMEA Central', assignedRepId: 'REP-103', repName: 'Elena Rostova', accountCount: 52, totalPipelinePotential: 5100000, workloadIndex: 1.55, quotaAttainmentPct: 124.0 },
    { territoryId: 'TERR-APAC-SOUTH', territoryName: 'APAC South', assignedRepId: 'REP-104', repName: 'Kenji Takahashi', accountCount: 24, totalPipelinePotential: 1850000, workloadIndex: 0.65, quotaAttainmentPct: 72.4 }
  ]);

  recommendations = signal<RebalanceMove[]>([
    { accountId: 'ACC-9041', accountName: 'Apex Logistics Global', fromRepName: 'Elena Rostova', toRepName: 'Kenji Takahashi', rationale: 'High APAC branch density; balances EMEA overload', expectedWorkloadDeltaPct: -19.3 },
    { accountId: 'ACC-8812', accountName: 'Beacon Health Systems', fromRepName: 'Sarah Jenkins', toRepName: 'Michael Chang', rationale: 'West coast HQ relocation; balances East overload', expectedWorkloadDeltaPct: -18.7 },
    { accountId: 'ACC-7740', accountName: 'CloudVanguard AI', fromRepName: 'Sarah Jenkins', toRepName: 'Michael Chang', rationale: 'Tech vertical specialization and pipeline equalization', expectedWorkloadDeltaPct: -12.4 }
  ]);

  simulateEquilibrium(): void {
    this.giniIndex.set(0.14);
    this.planStatus.set('Simulation Active: Gini 0.14');
  }

  applyRebalance(): void {
    this.planStatus.set('Scheduled (14-Day Grace)');
  }
}
