import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface MrrWaterfallData {
  startingMrr: number;
  newMrr: number;
  expansionMrr: number;
  contractionMrr: number;
  churnedMrr: number;
  netEndingMrr: number;
  quickRatio: number;
}

export interface CohortRow {
  cohortMonth: string;
  startingAccounts: number;
  retention: number[]; // offset 0 to 5
}

@Component({
  selector: 'app-executive-revenue-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="executive-revenue-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Executive Revenue Intelligence</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              Live GAAP & SaaS Metrics
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Real-time ARR/MRR waterfall decomposition, cohort churn decay matrices, and Net Revenue Retention (NRR).
          </p>
        </div>

        <div class="flex items-center gap-3">
          <span class="text-xs text-slate-400">Currency: USD ($) &bull; As of Q4 2026</span>
        </div>
      </div>

      <!-- KPI Executive Summary Cards -->
      <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4" data-testid="kpi-cards">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-400">ARR Run Rate</div>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">\${{ (arr() / 1000000) | number:'1.2-2' }}M</div>
          <div class="text-[11px] text-emerald-600 font-semibold mt-1">↑ +24.8% YoY</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-400">Current MRR</div>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">\${{ (mrr() / 1000) | number:'1.1-1' }}k</div>
          <div class="text-[11px] text-emerald-600 font-semibold mt-1">↑ +14.2% MoM</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-400">Net Retention (NRR)</div>
          <div class="text-xl font-bold text-indigo-600 mt-1 font-mono">{{ nrr() }}%</div>
          <div class="text-[11px] text-slate-500 mt-1">Top-quartile >115%</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-400">Gross Retention (GRR)</div>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">{{ grr() }}%</div>
          <div class="text-[11px] text-slate-500 mt-1">World-class benchmark</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-400">SaaS Quick Ratio</div>
          <div class="text-xl font-bold text-emerald-600 mt-1 font-mono">{{ waterfall().quickRatio }}x</div>
          <div class="text-[11px] text-emerald-600 font-medium mt-1">Healthy (> 4.0x)</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-400">LTV : CAC</div>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">4.8x</div>
          <div class="text-[11px] text-slate-500 mt-1">Payback 7.2 mo</div>
        </div>
      </div>

      <!-- MRR Waterfall Decomposition -->
      <div class="bg-white border rounded-xl p-6 shadow-sm space-y-4">
        <div class="flex items-center justify-between">
          <h3 class="font-bold text-slate-900 text-sm">Monthly Recurring Revenue (MRR) Waterfall</h3>
          <span class="text-xs text-slate-400">Net Delta: +\${{ (waterfall().netEndingMrr - waterfall().startingMrr) | number:'1.0-0' }}</span>
        </div>

        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 pt-2">
          <div class="p-3 bg-slate-50 rounded-lg border text-center">
            <span class="text-xs text-slate-500 block">Starting MRR</span>
            <span class="text-base font-bold text-slate-900 font-mono mt-1 block">\${{ waterfall().startingMrr | number:'1.0-0' }}</span>
          </div>

          <div class="p-3 bg-emerald-50 rounded-lg border border-emerald-100 text-center">
            <span class="text-xs text-emerald-700 block">New Bookings</span>
            <span class="text-base font-bold text-emerald-700 font-mono mt-1 block">+\${{ waterfall().newMrr | number:'1.0-0' }}</span>
          </div>

          <div class="p-3 bg-indigo-50 rounded-lg border border-indigo-100 text-center">
            <span class="text-xs text-indigo-700 block">Expansion</span>
            <span class="text-base font-bold text-indigo-700 font-mono mt-1 block">+\${{ waterfall().expansionMrr | number:'1.0-0' }}</span>
          </div>

          <div class="p-3 bg-amber-50 rounded-lg border border-amber-100 text-center">
            <span class="text-xs text-amber-800 block">Contraction</span>
            <span class="text-base font-bold text-amber-800 font-mono mt-1 block">-\${{ waterfall().contractionMrr | number:'1.0-0' }}</span>
          </div>

          <div class="p-3 bg-rose-50 rounded-lg border border-rose-100 text-center">
            <span class="text-xs text-rose-700 block">Customer Churn</span>
            <span class="text-base font-bold text-rose-700 font-mono mt-1 block">-\${{ waterfall().churnedMrr | number:'1.0-0' }}</span>
          </div>

          <div class="p-3 bg-slate-900 text-white rounded-lg border text-center">
            <span class="text-xs text-slate-300 block">Ending MRR</span>
            <span class="text-base font-bold text-emerald-400 font-mono mt-1 block">\${{ waterfall().netEndingMrr | number:'1.0-0' }}</span>
          </div>
        </div>
      </div>

      <!-- Cohort Retention & Churn Heatmap -->
      <div class="bg-white border rounded-xl p-6 shadow-sm space-y-4">
        <div class="flex items-center justify-between">
          <div>
            <h3 class="font-bold text-slate-900 text-sm">Customer Retention Cohort Matrix</h3>
            <p class="text-xs text-slate-400 mt-0.5">% Active Accounts retained by month offset since acquisition</p>
          </div>
          <span class="text-xs text-slate-400">Green shade indicates retention intensity</span>
        </div>

        <div class="overflow-x-auto" data-testid="cohort-table-wrapper">
          <table class="w-full text-xs text-left border-collapse">
            <thead>
              <tr class="border-b bg-slate-50 text-slate-600">
                <th class="py-2.5 px-3 font-semibold">Cohort</th>
                <th class="py-2.5 px-3 font-semibold text-center">Accounts</th>
                <th class="py-2.5 px-3 font-semibold text-center">M0</th>
                <th class="py-2.5 px-3 font-semibold text-center">M1</th>
                <th class="py-2.5 px-3 font-semibold text-center">M2</th>
                <th class="py-2.5 px-3 font-semibold text-center">M3</th>
                <th class="py-2.5 px-3 font-semibold text-center">M4</th>
                <th class="py-2.5 px-3 font-semibold text-center">M5</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (row of cohortRows; track row.cohortMonth) {
                <tr class="hover:bg-slate-50">
                  <td class="py-2 px-3 font-medium text-slate-800 font-mono">{{ row.cohortMonth }}</td>
                  <td class="py-2 px-3 text-center text-slate-500 font-mono">{{ row.startingAccounts }}</td>
                  @for (rate of row.retention; track $index) {
                    <td class="py-2 px-3 text-center font-mono font-medium"
                      [ngClass]="getRetentionColor(rate)">
                      {{ rate }}%
                    </td>
                  }
                  @for (empty of getRemainingCols(row.retention.length); track $index) {
                    <td class="py-2 px-3 text-center text-slate-300 font-mono">&mdash;</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class ExecutiveRevenueCockpitComponent {
  arr = signal<number>(1754400);
  mrr = signal<number>(146200);
  nrr = signal<number>(118.5);
  grr = signal<number>(95.6);

  waterfall = signal<MrrWaterfallData>({
    startingMrr: 125000,
    newMrr: 18500,
    expansionMrr: 8200,
    contractionMrr: 2100,
    churnedMrr: 3400,
    netEndingMrr: 146200,
    quickRatio: 4.85
  });

  cohortRows: CohortRow[] = [
    { cohortMonth: '2026-05', startingAccounts: 50, retention: [100, 96, 92, 89, 86, 84] },
    { cohortMonth: '2026-06', startingAccounts: 62, retention: [100, 97, 94, 91, 88] },
    { cohortMonth: '2026-07', startingAccounts: 74, retention: [100, 96, 93, 90] },
    { cohortMonth: '2026-08', startingAccounts: 86, retention: [100, 98, 95] },
    { cohortMonth: '2026-09', startingAccounts: 98, retention: [100, 97] },
    { cohortMonth: '2026-10', startingAccounts: 110, retention: [100] }
  ];

  getRemainingCols(length: number): number[] {
    return Array(Math.max(0, 6 - length)).fill(0);
  }

  getRetentionColor(rate: number): string {
    if (rate >= 95) return 'bg-emerald-100 text-emerald-800';
    if (rate >= 90) return 'bg-emerald-50 text-emerald-700';
    if (rate >= 85) return 'bg-amber-50 text-amber-800';
    return 'bg-rose-50 text-rose-800';
  }
}
