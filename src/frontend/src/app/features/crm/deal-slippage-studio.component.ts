import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DealRow {
  id: string;
  title: string;
  account: string;
  amount: number;
  stage: string;
  rep: string;
  daysInStage: number;
  slippageScore: number;
  risk: 'LowHealthy' | 'ModerateWatch' | 'HighSlipping' | 'CriticalStalled';
  missingGates: string[];
}

@Component({
  selector: 'app-deal-slippage-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-amber-500/10 text-amber-400 rounded-lg border border-amber-500/20 text-xl font-bold">🎯</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Deal Velocity & Stage-Gate Governance Radar</h1>
              <p class="text-slate-400 text-sm">Twenty-style opportunity stage gates, deal stall detection, probability decay & slippage forecast</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="openNewDealModal()" class="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-lg text-sm font-medium shadow-lg shadow-amber-900/30 transition">
            + New Opportunity Deal
          </button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Total Pipeline Value</div>
          <div class="text-3xl font-extrabold text-white">\${{ totalPipeline() | number }}</div>
          <div class="text-xs text-slate-400 mt-1">Across active stages</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">At-Risk Slipping Pipeline</div>
          <div class="text-3xl font-extrabold text-rose-400">\${{ atRiskAmount() | number }}</div>
          <div class="text-xs text-rose-500/80 mt-1">Score ≥ 50 or >30d stagnant</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Weighted Win Forecast</div>
          <div class="text-3xl font-extrabold text-emerald-400">\${{ weightedAmount() | number }}</div>
          <div class="text-xs text-emerald-400 mt-1">Slippage decay applied</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Stage-Gate Blockers</div>
          <div class="text-3xl font-extrabold text-amber-400">{{ blockedCount() }}</div>
          <div class="text-xs text-amber-400 mt-1">Missing required criteria</div>
        </div>
      </div>

      <!-- Main Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Deal Slippage Board -->
        <div class="lg:col-span-2 bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-white">Live Opportunity Radar</h2>
            <span class="text-xs text-slate-400">Strict stage-gate enforcement enabled</span>
          </div>

          <div class="space-y-3">
            <div *ngFor="let deal of deals()" class="p-4 bg-slate-800/80 border border-slate-700 rounded-lg hover:border-slate-600 transition flex items-start justify-between gap-4">
              <div class="flex-1">
                <div class="flex items-center gap-2 mb-1">
                  <span class="px-2 py-0.5 rounded text-xs font-bold uppercase bg-slate-700 text-slate-300">
                    {{ deal.stage }}
                  </span>
                  <span [class]="getRiskBadge(deal.risk)" class="px-2 py-0.5 rounded text-xs font-semibold">
                    Score: {{ deal.slippageScore }}/100 • {{ deal.risk }}
                  </span>
                  <span *ngIf="deal.missingGates.length > 0" class="px-2 py-0.5 rounded text-xs bg-rose-500/20 text-rose-300 border border-rose-500/30">
                    🔒 Blocked
                  </span>
                </div>
                <h3 class="text-base font-bold text-white">{{ deal.title }}</h3>
                <div class="text-xs text-slate-400 mt-1">
                  Account: <strong class="text-slate-200">{{ deal.account }}</strong> • Rep: {{ deal.rep }} • Dwell: <span [class.text-rose-400]="deal.daysInStage > 20">{{ deal.daysInStage }} days</span>
                </div>
                <!-- Missing Gates Notice -->
                <div *ngIf="deal.missingGates.length > 0" class="mt-2 text-xs text-rose-300 flex items-center gap-1.5">
                  <span>⚠️ Missing Gate:</span>
                  <span class="font-mono bg-rose-500/10 px-1.5 py-0.5 rounded">{{ deal.missingGates.join(', ') }}</span>
                </div>
              </div>

              <div class="text-right">
                <div class="text-lg font-extrabold text-white">\${{ deal.amount | number }}</div>
                <div class="flex gap-2 mt-2">
                  <button (click)="advanceDeal(deal)" class="px-3 py-1 bg-amber-600 hover:bg-amber-500 text-white rounded text-xs font-medium transition">
                    Advance
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Sidebar: Stage Gate Rules & Governance -->
        <div class="space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-3">Active Stage Gate Rules</h2>
            <div class="space-y-3 text-xs">
              <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg">
                <div class="font-semibold text-white mb-0.5">Needs Analysis → Proposal Quote</div>
                <div class="text-slate-400">Gate: <span class="text-amber-300">DecisionMakerIdentified</span></div>
                <div class="text-[11px] text-slate-500 mt-1">Requires confirmed Economic Buyer contact.</div>
              </div>
              <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg">
                <div class="font-semibold text-white mb-0.5">Proposal Quote → Negotiation</div>
                <div class="text-slate-400">Gate: <span class="text-amber-300">MarginThresholdMet (≥20%)</span></div>
                <div class="text-[11px] text-slate-500 mt-1">Prevents unprofitable discount quoting.</div>
              </div>
              <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg">
                <div class="font-semibold text-white mb-0.5">Negotiation → Closed Won</div>
                <div class="text-slate-400">Gate: <span class="text-amber-300">SignedContractAttached</span></div>
                <div class="text-[11px] text-slate-500 mt-1">Cryptographic e-signature verification required.</div>
              </div>
            </div>
          </div>

          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-2">Probability Time-Decay</h2>
            <p class="text-xs text-slate-400 leading-relaxed">
              When a deal's close date is repeatedly postponed or remains stagnant in stage beyond historical benchmarks, the system automatically applies non-linear decay to its win probability in the executive sales forecast.
            </p>
          </div>
        </div>
      </div>
    </div>
  `
})
export class DealSlippageStudioComponent {
  deals = signal<DealRow[]>([
    {
      id: 'd-1',
      title: 'Enterprise ERP Modernization Suite',
      account: 'Mediterranean Logistics Group',
      amount: 140000,
      stage: 'ProposalQuote',
      rep: 'Sami Ben Ali',
      daysInStage: 38,
      slippageScore: 82,
      risk: 'CriticalStalled',
      missingGates: ['MarginThresholdMet (14% < 20%)']
    },
    {
      id: 'd-2',
      title: 'WhatsApp Commerce Expansion (50 Channels)',
      account: 'Carthage Retailers',
      amount: 65000,
      stage: 'NegotiationReview',
      rep: 'Elena Rostova',
      daysInStage: 12,
      slippageScore: 28,
      risk: 'ModerateWatch',
      missingGates: ['SignedContractAttached']
    },
    {
      id: 'd-3',
      title: 'Cloud Contact Center Migration',
      account: 'Zenith Telecom',
      amount: 220000,
      stage: 'NeedsAnalysis',
      rep: 'Karim Mansour',
      daysInStage: 5,
      slippageScore: 10,
      risk: 'LowHealthy',
      missingGates: []
    }
  ]);

  totalPipeline = computed(() => this.deals().reduce((acc, d) => acc + d.amount, 0));
  atRiskAmount = computed(() => this.deals().filter(d => d.slippageScore >= 50).reduce((acc, d) => acc + d.amount, 0));
  weightedAmount = computed(() => Math.round(this.totalPipeline() * 0.42));
  blockedCount = computed(() => this.deals().filter(d => d.missingGates.length > 0).length);

  getRiskBadge(risk: string): string {
    switch (risk) {
      case 'CriticalStalled': return 'bg-rose-500/20 text-rose-400 border border-rose-500/30';
      case 'HighSlipping': return 'bg-amber-500/20 text-amber-400 border border-amber-500/30';
      case 'ModerateWatch': return 'bg-blue-500/20 text-blue-300 border border-blue-500/30';
      default: return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
    }
  }

  advanceDeal(deal: DealRow): void {
    // Demonstration action
  }

  openNewDealModal(): void {
    // Demonstration modal
  }
}
