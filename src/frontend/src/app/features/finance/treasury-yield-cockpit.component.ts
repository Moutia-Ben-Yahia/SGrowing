import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CashForecastPeriod {
  periodLabel: string;
  daysOut: number;
  projectedInflows: number;
  projectedOutflows: number;
  netCashChange: number;
  projectedEndingBalance: number;
  confidenceIntervalPct: number;
}

export interface TreasuryAllocation {
  instrumentId: string;
  instrumentName: string;
  currentAllocatedAmount: number;
  recommendedAllocationAmount: number;
  targetApyPercentage: number;
  projectedAnnualYield: number;
  liquidityDaysTPlus: number;
}

@Component({
  selector: 'app-treasury-yield-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="treasury-yield-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">Horizon 99</span>
            <span class="text-xs text-slate-400">Enterprise Treasury & Working Capital Yield Optimizer</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Treasury Cash Flow Forecasting & Yield Optimizer</h1>
          <p class="text-sm text-slate-400">Stochastic 30/60/90-day cash forecasting, liquidity reserve buffers, and automated surplus yield sweeps.</p>
        </div>
      </header>

      <!-- KPI Overview Cards -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-cash-reserve">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Liquid Cash Reserve</div>
          <div class="text-2xl font-bold text-white mt-1">\${{ cashReserve() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Operating Cash + Money Market</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-surplus-capital">
          <div class="text-xs font-medium text-slate-400 uppercase">Deployable Surplus Capital</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ surplusCapital() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Above \$2,500,000 liquidity buffer</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-blended-apy">
          <div class="text-xs font-medium text-slate-400 uppercase">Blended Portfolio APY</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ blendedApy() | number:'1.2-2' }}%</div>
          <div class="text-xs text-slate-400 mt-1">Projected \${{ totalAnnualYield() | number:'1.0-0' }} / yr yield</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-runway-months">
          <div class="text-xs font-medium text-slate-400 uppercase">Cash Runway</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ runwayMonths() | number:'1.1-1' }} Months</div>
          <div class="text-xs text-slate-400 mt-1">At current net burn trajectory</div>
        </div>
      </section>

      <!-- 30/60/90 Day Cash Forecasts -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <h2 class="text-lg font-semibold text-white mb-4">Monte Carlo Stochastic 30/60/90-Day Cash Flow Projections</h2>
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          @for (p of projections(); track p.periodLabel) {
            <div class="bg-slate-900/60 border border-slate-750 p-4 rounded-lg">
              <div class="flex items-center justify-between mb-2">
                <span class="font-semibold text-white text-sm">{{ p.periodLabel }}</span>
                <span class="text-xs text-slate-400">{{ p.confidenceIntervalPct }}% Conf.</span>
              </div>
              <div class="space-y-1 text-xs">
                <div class="flex justify-between">
                  <span class="text-slate-400">Inflows:</span>
                  <span class="text-emerald-400 font-mono">+\${{ p.projectedInflows | number:'1.0-0' }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">Outflows:</span>
                  <span class="text-rose-400 font-mono">-\${{ p.projectedOutflows | number:'1.0-0' }}</span>
                </div>
                <div class="flex justify-between border-t border-slate-800 pt-1 font-semibold">
                  <span class="text-slate-300">Ending Balance:</span>
                  <span class="text-cyan-400 font-mono">\${{ p.projectedEndingBalance | number:'1.0-0' }}</span>
                </div>
              </div>
            </div>
          }
        </div>
      </section>

      <!-- Treasury Allocations Grid -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Active Treasury Instruments & Automated Yield Sweep</h2>
          <span class="text-xs text-slate-400">Operating Reserve Buffer: \$2,500,000 (Locked)</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          @for (a of allocations(); track a.instrumentId) {
            <div class="bg-slate-900/60 border border-slate-750 rounded-lg p-4 flex flex-col justify-between"
                 [attr.data-testid]="'instrument-card-' + a.instrumentId">
              <div>
                <div class="flex items-center justify-between mb-2">
                  <span class="text-xs font-mono text-emerald-400">{{ a.instrumentId }}</span>
                  <span class="px-2 py-0.5 rounded text-xs font-bold bg-emerald-500/20 text-emerald-300">
                    {{ a.targetApyPercentage | number:'1.2-2' }}% APY
                  </span>
                </div>
                <div class="font-semibold text-white text-sm mb-1">{{ a.instrumentName }}</div>
                <div class="text-xs text-slate-400 mb-3">Liquidity: T+{{ a.liquidityDaysTPlus }} settlement</div>

                <div class="space-y-1 text-xs font-mono bg-slate-950/60 p-2.5 rounded mb-3">
                  <div class="flex justify-between">
                    <span class="text-slate-400">Allocated:</span>
                    <span class="text-white font-bold">\${{ a.currentAllocatedAmount | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-slate-400">Annual Yield:</span>
                    <span class="text-emerald-400">\${{ a.projectedAnnualYield | number:'1.0-0' }}/yr</span>
                  </div>
                </div>
              </div>

              <button class="w-full py-1.5 px-3 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-semibold transition-colors"
                      (click)="sweep(a.instrumentId)"
                      [attr.data-testid]="'btn-sweep-' + a.instrumentId">
                Sweep +\$250k Surplus
              </button>
            </div>
          }
        </div>
      </section>

      <!-- Action Toast -->
      @if (lastSweepNotice()) {
        <div class="p-3 bg-emerald-950/60 border border-emerald-500/40 rounded-xl text-emerald-300 text-xs flex items-center justify-between"
             data-testid="sweep-notice">
          <span>{{ lastSweepNotice() }}</span>
          <button (click)="lastSweepNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class TreasuryYieldCockpitComponent {
  readonly cashReserve = signal<number>(7450000);
  readonly operatingBuffer = 2500000;

  readonly projections = signal<CashForecastPeriod[]>([
    { periodLabel: '30-Day Outlook', daysOut: 30, projectedInflows: 1850000, projectedOutflows: 1120000, netCashChange: 730000, projectedEndingBalance: 8180000, confidenceIntervalPct: 96.5 },
    { periodLabel: '60-Day Outlook', daysOut: 60, projectedInflows: 3620000, projectedOutflows: 2280000, netCashChange: 1340000, projectedEndingBalance: 8790000, confidenceIntervalPct: 91.8 },
    { periodLabel: '90-Day Outlook', daysOut: 90, projectedInflows: 5840000, projectedOutflows: 3550000, netCashChange: 2290000, projectedEndingBalance: 9740000, confidenceIntervalPct: 87.2 }
  ]);

  readonly allocations = signal<TreasuryAllocation[]>([
    { instrumentId: 'US-TBILL-4W', instrumentName: 'US Treasury 4-Week Bills (Direct Auction)', currentAllocatedAmount: 2200000, recommendedAllocationAmount: 3000000, targetApyPercentage: 5.25, projectedAnnualYield: 115500, liquidityDaysTPlus: 1 },
    { instrumentId: 'INST-MMF-PRIME', instrumentName: 'Goldman Sachs Institutional Prime MMF', currentAllocatedAmount: 1800000, recommendedAllocationAmount: 2200000, targetApyPercentage: 5.10, projectedAnnualYield: 91800, liquidityDaysTPlus: 0 },
    { instrumentId: 'FED-REPO-OVERNIGHT', instrumentName: 'High-Yield Overnight Federal Repo Sweep', currentAllocatedAmount: 950000, recommendedAllocationAmount: 1250000, targetApyPercentage: 4.95, projectedAnnualYield: 47025, liquidityDaysTPlus: 0 }
  ]);

  readonly lastSweepNotice = signal<string | null>(null);

  readonly surplusCapital = computed(() =>
    Math.max(0, this.cashReserve() - this.operatingBuffer)
  );

  readonly totalAnnualYield = computed(() =>
    this.allocations().reduce((sum, a) => sum + a.projectedAnnualYield, 0)
  );

  readonly blendedApy = computed(() => {
    const totalAlloc = this.allocations().reduce((sum, a) => sum + a.currentAllocatedAmount, 0);
    return totalAlloc > 0 ? (this.totalAnnualYield() / totalAlloc) * 100 : 5.12;
  });

  readonly runwayMonths = computed(() => {
    const monthlyOpex = 1120000;
    return this.cashReserve() / monthlyOpex;
  });

  sweep(instrumentId: string): void {
    const sweepAmount = 250000;
    if (this.surplusCapital() < sweepAmount) return;

    this.allocations.update(list =>
      list.map(a => a.instrumentId === instrumentId ? {
        ...a,
        currentAllocatedAmount: a.currentAllocatedAmount + sweepAmount,
        projectedAnnualYield: Math.round((a.currentAllocatedAmount + sweepAmount) * (a.targetApyPercentage / 100))
      } : a)
    );

    this.cashReserve.update(r => r - sweepAmount);
    this.lastSweepNotice.set(`Automated Sweep: Allocated \$${sweepAmount.toLocaleString()} into ${instrumentId}. Current Surplus: \$${this.surplusCapital().toLocaleString()}.`);
  }
}
