import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ExpansionAccount {
  accountId: string;
  accountName: string;
  currentArr: number;
  healthScore: number;
  featureAdoptionRate: number;
  seatUtilizationRate: number;
  predictedExpansionArr: number;
  predictedNrr: number;
  recommendedPlay: string;
  proposalGenerated: boolean;
}

@Component({
  selector: 'app-account-expansion-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="account-expansion-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-purple-500/20 text-purple-400 border border-purple-500/30">Horizon 86</span>
            <span class="text-xs text-slate-400">Account Expansion & NRR Radar</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Account Expansion & Upsell Opportunity Radar</h1>
          <p class="text-sm text-slate-400">Predictive Net Revenue Retention (NRR) modeling, seat saturation detection, and automated upsell proposals.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-expansion-pipeline">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Expansion Pipeline</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ totalExpansionArr() | number:'1.0-0' }} ARR</div>
          <div class="text-xs text-slate-400 mt-1">High-propensity upsell opportunities</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-predicted-nrr">
          <div class="text-xs font-medium text-slate-400 uppercase">Portfolio Predicted NRR</div>
          <div class="text-2xl font-bold text-purple-400 mt-1">{{ averageNrr() | number:'1.1-1' }}%</div>
          <div class="text-xs text-slate-400 mt-1">Target benchmark: > 120%</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-ready-accounts">
          <div class="text-xs font-medium text-slate-400 uppercase">Expansion-Ready Accounts</div>
          <div class="text-2xl font-bold text-white mt-1">{{ expansionReadyCount() }} / {{ accounts().length }}</div>
          <div class="text-xs text-slate-400 mt-1">Utilization > 85% or Health > 80</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-proposals-dispatched">
          <div class="text-xs font-medium text-slate-400 uppercase">Proposals Dispatched</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ proposalsCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Algorithmic quotes in progress</div>
        </div>
      </section>

      <!-- Account Radar Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Accounts with Expansion Signals</h2>
          <div class="space-y-3">
            @for (acc of accounts(); track acc.accountId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedAccount()?.accountId === acc.accountId"
                [class.ring-purple-500]="selectedAccount()?.accountId === acc.accountId"
                (click)="selectAccount(acc)"
                data-testid="account-expansion-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="font-semibold text-white text-base">{{ acc.accountName }}</h3>
                    <p class="text-xs text-slate-400">Current ARR: \${{ acc.currentArr | number:'1.0-0' }} &bull; Health: {{ acc.healthScore }}/100</p>
                  </div>
                  <div class="text-right">
                    <span class="text-xs font-bold text-emerald-400">+\${{ acc.predictedExpansionArr | number:'1.0-0' }} ARR</span>
                    <div class="text-xs text-purple-300">NRR: {{ acc.predictedNrr }}%</div>
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-2 mt-3 pt-3 border-t border-slate-700/60 text-xs">
                  <div>
                    <span class="text-slate-400">Seat Utilization:</span>
                    <span class="font-semibold text-slate-200 ml-1">{{ acc.seatUtilizationRate }}%</span>
                  </div>
                  <div>
                    <span class="text-slate-400">Feature Adoption:</span>
                    <span class="font-semibold text-slate-200 ml-1">{{ acc.featureAdoptionRate }}%</span>
                  </div>
                </div>

                <div class="mt-3 flex items-center justify-between pt-2">
                  <span class="text-xs px-2 py-0.5 rounded bg-slate-700 text-slate-300">Play: {{ acc.recommendedPlay }}</span>
                  @if (acc.proposalGenerated) {
                    <span class="text-xs px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30" data-testid="proposal-success-badge">
                      Proposal Dispatched
                    </span>
                  } @else {
                    <button
                      (click)="generateProposal(acc.accountId); $event.stopPropagation()"
                      data-testid="btn-generate-proposal"
                      class="px-3 py-1 bg-purple-600 hover:bg-purple-500 text-white text-xs font-semibold rounded shadow transition-colors"
                    >
                      Generate Proposal
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Expansion Intelligence Detail</h2>
            @if (selectedAccount(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Selected Entity</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.accountName }}</div>
                  <div class="text-xs text-purple-400 mt-1">Recommended Action: {{ sel.recommendedPlay }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Base Contract:</span>
                    <span class="font-mono text-white">\${{ sel.currentArr | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Upsell Lift:</span>
                    <span class="font-mono text-emerald-400">+\${{ sel.predictedExpansionArr | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Expansion Score:</span>
                    <span class="font-mono text-purple-300">{{ sel.healthScore }} / 100</span>
                  </div>
                </div>

                @if (!sel.proposalGenerated) {
                  <button
                    (click)="generateProposal(sel.accountId)"
                    class="w-full mt-4 py-2 bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white font-semibold rounded text-sm shadow transition-all"
                  >
                    Launch Automated Deal Desk Play
                  </button>
                }
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select an account from the radar list to view deep metrics.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-purple-400 font-semibold">Autonomous Deal Desk:</span> Automatically coordinates with Deal Copilot and CRM Metadata to inject quotes.
          </div>
        </div>
      </section>
    </div>
  `
})
export class AccountExpansionCockpitComponent {
  accounts = signal<ExpansionAccount[]>([
    {
      accountId: 'acc_ent_acme_corp',
      accountName: 'Acme Global Technologies',
      currentArr: 120000,
      healthScore: 92,
      featureAdoptionRate: 88,
      seatUtilizationRate: 98,
      predictedExpansionArr: 45000,
      predictedNrr: 137.5,
      recommendedPlay: 'Tier Upgrade: Enterprise Platinum + 50 Seats',
      proposalGenerated: false
    },
    {
      accountId: 'acc_med_biotech_ai',
      accountName: 'BioSynthetix Genomics',
      currentArr: 84000,
      healthScore: 86,
      featureAdoptionRate: 79,
      seatUtilizationRate: 91,
      predictedExpansionArr: 28000,
      predictedNrr: 133.3,
      recommendedPlay: 'Addon: Autonomous Vector RAG & Deep Research',
      proposalGenerated: false
    },
    {
      accountId: 'acc_fin_hyperpay',
      accountName: 'HyperPay Merchant Services',
      currentArr: 195000,
      healthScore: 95,
      featureAdoptionRate: 94,
      seatUtilizationRate: 99,
      predictedExpansionArr: 75000,
      predictedNrr: 138.4,
      recommendedPlay: 'Dedicated Multi-Region Shard & SLA Tier 1',
      proposalGenerated: false
    }
  ]);

  selectedAccount = signal<ExpansionAccount | null>(this.accounts()[0]);

  totalExpansionArr = computed(() =>
    this.accounts().reduce((sum, a) => sum + a.predictedExpansionArr, 0)
  );

  averageNrr = computed(() => {
    const list = this.accounts();
    if (!list.length) return 0;
    return list.reduce((sum, a) => sum + a.predictedNrr, 0) / list.length;
  });

  expansionReadyCount = computed(() =>
    this.accounts().filter(a => a.seatUtilizationRate >= 85 || a.healthScore >= 80).length
  );

  proposalsCount = computed(() =>
    this.accounts().filter(a => a.proposalGenerated).length
  );

  selectAccount(account: ExpansionAccount): void {
    this.selectedAccount.set(account);
  }

  generateProposal(accountId: string): void {
    this.accounts.update(list =>
      list.map(a => a.accountId === accountId ? { ...a, proposalGenerated: true } : a)
    );
    if (this.selectedAccount()?.accountId === accountId) {
      this.selectedAccount.update(curr => curr ? { ...curr, proposalGenerated: true } : null);
    }
  }
}
