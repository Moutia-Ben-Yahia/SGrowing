import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface DealItem {
  dealId: string;
  accountName: string;
  amount: number;
  stage: string;
  daysInactive: number;
  winProbability: number;
  riskLevel: 'Healthy' | 'ModerateRisk' | 'SevereRisk';
  prescribedAction: string;
  assignedAe: string;
}

@Component({
  selector: 'app-deal-copilot-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="deal-copilot-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">RevOps Deal Copilot & AI Risk Radar</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800">
              Bayesian Win Rate Forecasting
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Predict silent deal slippage, evaluate engagement inactivity heuristics, and prescribe autonomous recovery actions.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <div class="text-right">
            <span class="text-xs text-slate-400 block">Total Pipeline Tracked</span>
            <span class="text-sm font-bold text-slate-900 font-mono">\${{ totalPipeline() | number:'1.0-0' }}</span>
          </div>
        </div>
      </div>

      <!-- KPI Executive Summary Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="deal-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Aggregate Pipeline Health</span>
          <div class="text-xl font-bold text-emerald-600 mt-1 font-mono">{{ aggregateHealth() }}%</div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">Weighted Probability</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">At-Risk Deals</span>
          <div class="text-xl font-bold text-amber-600 mt-1 font-mono">{{ atRiskCount() }}</div>
          <span class="text-[11px] text-slate-500 mt-1 block">Inactivity &gt; 5 days</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">AI Interventions Executed</span>
          <div class="text-xl font-bold text-indigo-600 mt-1 font-mono">{{ interventionsCount() }}</div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">+20% Avg Win Rate Lift</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Forecasted Revenue</span>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">\${{ (forecastedRevenue() / 1000) | number:'1.0-0' }}k</div>
          <span class="text-[11px] text-slate-500 mt-1 block">Probability-Adjusted</span>
        </div>
      </div>

      <!-- Action Toast Banner -->
      @if (actionToast()) {
        <div class="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-xs text-emerald-800 font-medium flex items-center justify-between" data-testid="deal-toast">
          <span>✓ {{ actionToast() }}</span>
          <button (click)="actionToast.set('')" class="text-emerald-700 hover:text-emerald-900 font-bold">&times;</button>
        </div>
      }

      <!-- Deals Pipeline List -->
      <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
        <div class="flex items-center justify-between">
          <h3 class="font-bold text-slate-900 text-sm">Active Sales Pipeline & Risk Radar</h3>
          <span class="text-xs text-slate-400">Prescriptive Autonomous Next Steps</span>
        </div>

        <div class="space-y-4" data-testid="deals-list">
          @for (deal of deals(); track deal.dealId) {
            <div class="p-4 border rounded-xl bg-slate-50/50 hover:bg-white transition-colors space-y-3">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <div>
                  <div class="flex items-center gap-2">
                    <h4 class="font-bold text-slate-900 text-sm">{{ deal.accountName }}</h4>
                    <span class="px-2 py-0.5 rounded text-[11px] font-semibold"
                      [ngClass]="getRiskBadgeClass(deal.riskLevel)">
                      {{ deal.riskLevel }}
                    </span>
                  </div>
                  <div class="flex items-center gap-2 text-xs text-slate-500 mt-0.5">
                    <span>Stage: <strong class="text-slate-700">{{ deal.stage }}</strong></span>
                    <span>&bull;</span>
                    <span>Owner: {{ deal.assignedAe }}</span>
                    <span>&bull;</span>
                    <span [ngClass]="deal.daysInactive > 5 ? 'text-rose-600 font-semibold' : 'text-slate-500'">
                      {{ deal.daysInactive === 0 ? 'Active today' : deal.daysInactive + 'd inactive' }}
                    </span>
                  </div>
                </div>

                <div class="flex items-center gap-4 text-right">
                  <div>
                    <span class="text-xs text-slate-400 block">Deal Value</span>
                    <span class="text-base font-bold text-slate-900 font-mono">\${{ deal.amount | number:'1.0-0' }}</span>
                  </div>
                  <div>
                    <span class="text-xs text-slate-400 block">Win Rate</span>
                    <span class="text-base font-bold text-indigo-600 font-mono">{{ deal.winProbability }}%</span>
                  </div>
                </div>
              </div>

              <!-- AI Copilot Prescribed Action Card -->
              <div class="p-3 bg-indigo-50/60 rounded-lg border border-indigo-100 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
                <div>
                  <span class="font-bold text-indigo-900 flex items-center gap-1.5 mb-0.5">
                    🤖 Copilot Prescribed Intervention:
                  </span>
                  <span class="text-indigo-800">{{ deal.prescribedAction }}</span>
                </div>

                <button 
                  (click)="executeIntervention(deal)"
                  [disabled]="deal.riskLevel === 'Healthy' && deal.daysInactive === 0"
                  class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 disabled:bg-slate-300 text-white rounded font-medium text-xs transition-colors shadow-sm shrink-0"
                  [attr.data-testid]="'btn-intervene-' + deal.dealId">
                  Execute Intervention
                </button>
              </div>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class DealCopilotCockpitComponent {
  deals = signal<DealItem[]>([
    {
      dealId: 'deal_quantum_corp',
      accountName: 'Quantum Logistics International',
      amount: 185000,
      stage: 'Negotiation',
      daysInactive: 12,
      winProbability: 45,
      riskLevel: 'SevereRisk',
      prescribedAction: 'Draft multi-agent executive re-engagement memo highlighting air-gapped security advantages',
      assignedAe: 'Jessica Taylor'
    },
    {
      dealId: 'deal_fintech_hyper',
      accountName: 'HyperPay Global Remittances',
      amount: 320000,
      stage: 'Demo',
      daysInactive: 7,
      winProbability: 60,
      riskLevel: 'ModerateRisk',
      prescribedAction: 'Schedule multi-stakeholder economic impact ROI session with VP Finance',
      assignedAe: 'Jessica Taylor'
    },
    {
      dealId: 'deal_acme_retail',
      accountName: 'Acme Global Retail Systems',
      amount: 94000,
      stage: 'Proposal',
      daysInactive: 2,
      winProbability: 82,
      riskLevel: 'Healthy',
      prescribedAction: 'Transmit verified cryptographic PKCS#7 security and architecture packet',
      assignedAe: 'Marcus Vance'
    }
  ]);

  interventionsCount = signal<number>(3);
  actionToast = signal<string>('');

  totalPipeline = computed(() => {
    return this.deals().reduce((acc, d) => acc + d.amount, 0);
  });

  forecastedRevenue = computed(() => {
    return this.deals().reduce((acc, d) => acc + (d.amount * (d.winProbability / 100)), 0);
  });

  atRiskCount = computed(() => {
    return this.deals().filter(d => d.riskLevel !== 'Healthy').length;
  });

  aggregateHealth = computed(() => {
    const list = this.deals();
    if (!list.length) return 0;
    return Math.round(list.reduce((acc, d) => acc + d.winProbability, 0) / list.length);
  });

  getRiskBadgeClass(risk: string): string {
    switch (risk) {
      case 'SevereRisk': return 'bg-rose-100 text-rose-800';
      case 'ModerateRisk': return 'bg-amber-100 text-amber-800';
      case 'Healthy': return 'bg-emerald-100 text-emerald-800';
      default: return 'bg-slate-100 text-slate-800';
    }
  }

  executeIntervention(deal: DealItem): void {
    this.deals.update(list => list.map(d => {
      if (d.dealId === deal.dealId) {
        return {
          ...d,
          winProbability: Math.min(95, d.winProbability + 20),
          riskLevel: 'Healthy',
          daysInactive: 0
        };
      }
      return d;
    }));

    this.interventionsCount.update(c => c + 1);
    this.actionToast.set(`Successfully dispatched AI intervention for ${deal.accountName}. Win probability lifted by +20%.`);
  }
}
