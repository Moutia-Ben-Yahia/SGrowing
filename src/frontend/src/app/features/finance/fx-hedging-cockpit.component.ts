import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FxForwardRecord {
  contractId: string;
  corridor: string;
  lockedExchangeRate: number;
  notionalAmountSource: number;
  guaranteedAmountUsd: number;
  expiryDate: string;
  isSettled: boolean;
}

@Component({
  selector: 'app-fx-hedging-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="fx-hedging-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">Horizon 84</span>
            <span class="text-xs text-slate-400">Global FinOps & Settlement</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous B2B Multi-Currency FX Hedging & Settlement Escrow</h1>
          <p class="text-sm text-slate-400">Forward exchange rate locking, automated currency corridor hedging, and zero-slippage cross-border contract payouts.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="lockNewForward()" data-testid="lock-forward-btn" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-medium text-sm transition-colors shadow">
            + Lock New Forward Contract
          </button>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-hedged">
          <div class="text-xs font-medium text-slate-400 uppercase">Hedged Notional In Escrow</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ totalHedgedUsd() | number }}</div>
          <div class="text-xs text-slate-400 mt-1">Across {{ activeContractsCount() }} Forward Contracts</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-var-risk">
          <div class="text-xs font-medium text-slate-400 uppercase">Mesh Value-at-Risk (99% CI)</div>
          <div class="text-2xl font-bold text-white mt-1">1.8%</div>
          <div class="text-xs text-slate-400 mt-1">Standard corridor variance shielded</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-cumulative-savings">
          <div class="text-xs font-medium text-slate-400 uppercase">Cumulative Slippage Prevented</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">\${{ cumulativeSavingsUsd() | number }}</div>
          <div class="text-xs text-slate-400 mt-1">Protected from adverse FX fluctuations</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-settled-contracts">
          <div class="text-xs font-medium text-slate-400 uppercase">Contracts Successfully Settled</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ settledCount() }} Settled</div>
          <div class="text-xs text-slate-400 mt-1">Zero cross-border wire delays</div>
        </div>
      </section>

      <!-- Active Contracts Table -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-base font-semibold text-white">Active Multi-Currency Forward Escrow Contracts</h2>
          <span class="text-xs text-slate-400">Total Contracts: {{ contracts().length }}</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="contracts-table">
            <thead class="text-xs text-slate-400 uppercase border-b border-slate-700/60 bg-slate-800/40">
              <tr>
                <th class="p-3">Contract ID</th>
                <th class="p-3">Corridor</th>
                <th class="p-3">Source Notional</th>
                <th class="p-3">Locked Rate</th>
                <th class="p-3">Guaranteed USD</th>
                <th class="p-3">Maturity Date</th>
                <th class="p-3">Status</th>
                <th class="p-3">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-700/40">
              @for (c of contracts(); track c.contractId) {
                <tr class="hover:bg-slate-700/20" [attr.data-testid]="'contract-row-' + c.contractId">
                  <td class="p-3 font-mono text-xs text-slate-300">{{ c.contractId }}</td>
                  <td class="p-3 font-semibold text-white">{{ c.corridor }}</td>
                  <td class="p-3 text-slate-300">{{ c.notionalAmountSource | number }}</td>
                  <td class="p-3 font-mono text-xs text-emerald-400">{{ c.lockedExchangeRate }}</td>
                  <td class="p-3 font-bold text-white">\${{ c.guaranteedAmountUsd | number }}</td>
                  <td class="p-3 text-xs text-slate-400">{{ c.expiryDate | date:'mediumDate' }}</td>
                  <td class="p-3">
                    <span [ngClass]="{
                      'bg-emerald-500/20 text-emerald-400 border-emerald-500/30': !c.isSettled,
                      'bg-slate-700 text-slate-400 border-slate-600': c.isSettled
                    }" class="px-2 py-0.5 text-xs rounded border font-medium">
                      {{ c.isSettled ? 'Settled' : 'Active Forward' }}
                    </span>
                  </td>
                  <td class="p-3">
                    @if (!c.isSettled) {
                      <button (click)="settleContract(c)" [attr.data-testid]="'settle-btn-' + c.contractId" class="text-xs px-2.5 py-1 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-medium transition-colors">
                        Settle Escrow
                      </button>
                    } @else {
                      <span class="text-xs text-slate-500 font-medium">Payout Complete</span>
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
export class FxHedgingCockpitComponent {
  contracts = signal<FxForwardRecord[]>([
    {
      contractId: 'fwd_eur_701',
      corridor: 'EUR/USD',
      lockedExchangeRate: 1.0850,
      notionalAmountSource: 250000,
      guaranteedAmountUsd: 271250,
      expiryDate: '2026-12-15T00:00:00Z',
      isSettled: false
    },
    {
      contractId: 'fwd_gbp_702',
      corridor: 'GBP/USD',
      lockedExchangeRate: 1.2950,
      notionalAmountSource: 180000,
      guaranteedAmountUsd: 233100,
      expiryDate: '2026-12-30T00:00:00Z',
      isSettled: false
    }
  ]);

  cumulativeSavingsUsd = signal<number>(14250);

  totalHedgedUsd = computed(() => this.contracts()
    .filter(c => !c.isSettled)
    .reduce((sum, c) => sum + c.guaranteedAmountUsd, 0));

  activeContractsCount = computed(() => this.contracts().filter(c => !c.isSettled).length);
  settledCount = computed(() => this.contracts().filter(c => c.isSettled).length);

  settleContract(contract: FxForwardRecord) {
    this.contracts.update(list => list.map(c => {
      if (c.contractId === contract.contractId) {
        return { ...c, isSettled: true };
      }
      return c;
    }));
    this.cumulativeSavingsUsd.update(s => s + 4800);
  }

  lockNewForward() {
    const newContract: FxForwardRecord = {
      contractId: `fwd_sgd_${Date.now().toString().slice(-4)}`,
      corridor: 'SGD/USD',
      lockedExchangeRate: 0.7650,
      notionalAmountSource: 300000,
      guaranteedAmountUsd: 229500,
      expiryDate: '2027-01-30T00:00:00Z',
      isSettled: false
    };

    this.contracts.update(list => [newContract, ...list]);
  }
}
