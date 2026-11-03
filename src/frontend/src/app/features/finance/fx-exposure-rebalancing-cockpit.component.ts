import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface CurrencyPositionItem {
  currencyCode: string;
  grossReceivables: number;
  grossPayables: number;
  netUnhedgedExposure: number;
  currentSpotRate: number;
  valueAtRisk95Pct: number;
  unhedgedDriftPct: number;
}

export interface ForwardContractItem {
  contractId: string;
  currencyPair: string;
  notionalAmount: number;
  lockedForwardRate: number;
  counterpartyBank: string;
  accountingTreatment: string;
}

@Component({
  selector: 'app-fx-exposure-rebalancing-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="fx-exposure-rebalancing-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              Horizon 114 • Domain H19 & G15
            </span>
            <span class="text-xs text-slate-400">Dynamic Multi-Currency VaR &amp; Algorithmic FX Hedging</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">Dynamic Multi-Currency FX Hedging &amp; Exposure Mesh</h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-time Value-at-Risk monitoring, rolling forward hedge execution, and GAAP/IFRS margin protection.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="evaluateCurrencyRisk()"
            data-testid="evaluate-risk-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Evaluate 95% VaR Drift
          </button>
          <button
            (click)="executeForwardHedge()"
            data-testid="execute-hedge-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-emerald-600/30 flex items-center gap-2">
            Issue Forward Hedge Contract
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Portfolio 95% VaR (USD)</span>
          <p class="text-2xl font-bold text-amber-400 mt-1" data-testid="metric-portfolio-var">\${{ totalPortfolioVar() | number:'1.2-2' }}</p>
          <span class="text-xs text-slate-400 mt-1 block">Parametric 30-Day Horizon</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Hedged Notional Volume</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-hedged-notional">\${{ totalHedgedNotional() | number:'1.2-2' }}</p>
          <span class="text-xs text-indigo-300 mt-1 block">Active Forward Rolling Contracts</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Portfolio Hedge Ratio</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-hedge-ratio">{{ hedgeRatio() }}%</p>
          <span class="text-xs text-emerald-300 mt-1 block">Target Policy: &ge; 80%</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Maximum Allowed Drift</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-max-drift">&le; {{ maxDrift() }}%</p>
          <span class="text-xs text-slate-400 mt-1 block">Autopilot Rebalancing Trigger</span>
        </div>
      </div>

      <!-- Currency Exposure Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Foreign Currency Exposure Positions</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Currency</th>
                <th class="py-3 px-4 text-right">Gross Receivables</th>
                <th class="py-3 px-4 text-right">Gross Payables</th>
                <th class="py-3 px-4 text-right">Net Exposure</th>
                <th class="py-3 px-4 text-right">Spot (USD)</th>
                <th class="py-3 px-4 text-right">95% VaR</th>
                <th class="py-3 px-4 text-right">Unhedged Drift</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (exp of exposures(); track exp.currencyCode) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-bold text-white">{{ exp.currencyCode }}</td>
                  <td class="py-3 px-4 text-right text-slate-300">{{ exp.grossReceivables | number }}</td>
                  <td class="py-3 px-4 text-right text-slate-400">{{ exp.grossPayables | number }}</td>
                  <td class="py-3 px-4 text-right font-semibold text-white">{{ exp.netUnhedgedExposure | number }}</td>
                  <td class="py-3 px-4 text-right font-mono text-xs text-indigo-300">{{ exp.currentSpotRate | number:'1.4-4' }}</td>
                  <td class="py-3 px-4 text-right font-bold text-amber-400">\${{ exp.valueAtRisk95Pct | number }}</td>
                  <td class="py-3 px-4 text-right font-semibold" [class.text-rose-400]="exp.unhedgedDriftPct > 5.0" [class.text-emerald-400]="exp.unhedgedDriftPct <= 5.0">
                    {{ exp.unhedgedDriftPct }}%
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
export class FxExposureRebalancingCockpitComponent {
  totalPortfolioVar = signal<number>(589000);
  totalHedgedNotional = signal<number>(6000000);
  hedgeRatio = signal<number>(84.5);
  maxDrift = signal<number>(5.0);

  exposures = signal<CurrencyPositionItem[]>([
    { currencyCode: 'EUR', grossReceivables: 4200000, grossPayables: 1200000, netUnhedgedExposure: 3000000, currentSpotRate: 1.0850, valueAtRisk95Pct: 185000, unhedgedDriftPct: 4.2 },
    { currencyCode: 'GBP', grossReceivables: 2800000, grossPayables: 950000, netUnhedgedExposure: 1850000, currentSpotRate: 1.2940, valueAtRisk95Pct: 142000, unhedgedDriftPct: 6.8 },
    { currencyCode: 'JPY', grossReceivables: 450000000, grossPayables: 90000000, netUnhedgedExposure: 360000000, currentSpotRate: 0.0065, valueAtRisk95Pct: 210000, unhedgedDriftPct: 8.5 },
    { currencyCode: 'SGD', grossReceivables: 1800000, grossPayables: 600000, netUnhedgedExposure: 1200000, currentSpotRate: 0.7620, valueAtRisk95Pct: 52000, unhedgedDriftPct: 2.1 }
  ]);

  evaluateCurrencyRisk(): void {
    this.hedgeRatio.set(89.2);
  }

  executeForwardHedge(): void {
    this.totalHedgedNotional.update(v => v + 1500000);
    this.exposures.update(list =>
      list.map(e => e.currencyCode === 'GBP' ? { ...e, unhedgedDriftPct: 3.1 } : e)
    );
  }
}
