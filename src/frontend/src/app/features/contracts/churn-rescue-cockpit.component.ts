import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ChurnRiskAssessment {
  accountId: string;
  accountName: string;
  annualRecurringRevenue: number;
  churnPropensityScore: number;
  riskLevel: 'SevereCritical' | 'High' | 'Moderate' | 'Low';
  primaryRiskDrivers: string[];
  daysUntilRenewal: number;
  recommendedPlaybook: string;
}

export interface RenewalAgreement {
  agreementId: string;
  accountId: string;
  annualContractValue: number;
  discountAppliedPercent: number;
  priceLockYears: number;
  signatureStatus: string;
}

@Component({
  selector: 'app-churn-rescue-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="churn-rescue-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-cyan-400">
            <span class="inline-block w-2 h-2 rounded-full bg-cyan-400 animate-pulse"></span>
            Revenue Retention & Contracts • Horizon 50
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Predictive Churn Rescue & Contract Renewal Escrow
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Multi-factor churn risk propensity radar, automated retention playbooks, and dynamic price-lock renewal agreements.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="generateRenewalAgreement()"
            data-testid="generate-agreement-btn"
            class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-cyan-600/20"
          >
            <span>📜</span> Generate Renewal Escrow
          </button>
        </div>
      </div>

      <!-- Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">ARR at Risk</div>
          <div class="text-2xl font-bold text-rose-400 mt-1" data-testid="stat-arr-risk">
            \${{ totalArrAtRisk() | number:'1.0-0' }}
          </div>
          <div class="text-slate-400 text-xs mt-1">{{ highRiskCount() }} High-Risk Accounts</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Rescued ARR</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-rescued-arr">\${{ rescuedArr() | number:'1.0-0' }}</div>
          <div class="text-emerald-400 text-xs mt-1">14 Contracts Secured</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Net Revenue Retention</div>
          <div class="text-2xl font-bold text-white mt-1">114.6%</div>
          <div class="text-emerald-400 text-xs mt-1">Top Decile B2B SaaS</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Playbook Conversion</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">87.2%</div>
          <div class="text-slate-400 text-xs mt-1">Price-Lock Retention ROI</div>
        </div>
      </div>

      <!-- Main Stage: Churn Risk Radar & Playbook Execution Console -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Risk Radar Table (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Accounts at Risk Radar</h2>
            <span class="text-xs text-slate-400">Sorted by Churn Propensity Score</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (acc of accounts(); track acc.accountId) {
              <div
                (click)="selectAccount(acc)"
                [class.bg-slate-700/40]="selectedAccount()?.accountId === acc.accountId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'account-row-' + acc.accountId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ acc.accountName }}</span>
                    <span
                      [class]="getRiskBadgeClass(acc.riskLevel)"
                      class="px-2 py-0.5 rounded text-[10px] font-bold border uppercase"
                      [attr.data-testid]="'risk-badge-' + acc.accountId"
                    >
                      {{ acc.riskLevel }} ({{ acc.churnPropensityScore }}%)
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    ARR: <strong class="text-white">\${{ acc.annualRecurringRevenue | number:'1.0-0' }}</strong> • Renewal in:
                    <span [class]="acc.daysUntilRenewal < 35 ? 'text-amber-400 font-bold' : 'text-slate-300'">
                      {{ acc.daysUntilRenewal }} days
                    </span>
                  </div>
                  <div class="text-[11px] text-slate-500 mt-0.5">
                    Recommended: <span class="text-cyan-400 font-medium">{{ acc.recommendedPlaybook }}</span>
                  </div>
                </div>

                <div class="flex flex-col items-end gap-1 self-end md:self-auto">
                  <div class="text-[11px] text-slate-400">Propensity Meter</div>
                  <div class="w-24 bg-slate-700 rounded-full h-2 overflow-hidden">
                    <div
                      class="h-full rounded-full"
                      [class]="acc.churnPropensityScore >= 75 ? 'bg-rose-500' : 'bg-amber-500'"
                      [style.width]="acc.churnPropensityScore + '%'"
                    ></div>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Selected Account Playbook & Agreement Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          @if (selectedAccount(); as active) {
            <div class="space-y-4">
              <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
                <div>
                  <h3 class="font-bold text-white text-base">Retention Playbook Console</h3>
                  <p class="text-xs text-slate-400">{{ active.accountName }}</p>
                </div>
                <span class="text-xs px-2.5 py-1 rounded bg-slate-900 border border-slate-700 text-slate-300 font-mono">
                  \${{ active.annualRecurringRevenue | number:'1.0-0' }} ARR
                </span>
              </div>

              <!-- Risk Drivers Diagnostic Box -->
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-1.5 text-xs">
                <div class="font-semibold text-slate-300 uppercase text-[11px]">Primary Risk Drivers:</div>
                <ul class="list-disc list-inside text-rose-300 space-y-1">
                  @for (driver of active.primaryRiskDrivers; track driver) {
                    <li>{{ driver }}</li>
                  }
                </ul>
              </div>

              <!-- Playbook Execution Form -->
              <div class="space-y-3 text-xs">
                <div>
                  <label class="block text-slate-300 font-bold mb-1">Playbook Strategy</label>
                  <select
                    [(ngModel)]="active.recommendedPlaybook"
                    class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:border-cyan-500"
                  >
                    <option value="DiscountLock">Automated 15% 2-Year Price-Lock Escrow</option>
                    <option value="ExecutiveSponsorSync">Executive Sponsor VIP Briefing</option>
                    <option value="UsageCredits">$5,000 Promotional Platform Credits</option>
                  </select>
                </div>

                <div class="flex items-center justify-between gap-3 pt-2">
                  <div class="text-slate-400 text-[11px]">
                    Action Target: <strong class="text-white">{{ active.accountName }}</strong>
                  </div>
                  <button
                    (click)="executePlaybook(active)"
                    data-testid="execute-playbook-btn"
                    class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-xs font-semibold shadow-lg transition-colors"
                  >
                    Execute Rescue Playbook
                  </button>
                </div>
              </div>

              <!-- Active Renewal Agreement Escrow Summary -->
              @if (renewalAgreement(); as agreement) {
                <div class="pt-3 border-t border-slate-700/60 space-y-2 text-xs" data-testid="escrow-agreement-card">
                  <div class="flex items-center justify-between">
                    <span class="font-bold text-white">Contract Renewal Escrow</span>
                    <span class="bg-emerald-900/60 text-emerald-300 border border-emerald-700/60 px-2 py-0.5 rounded text-[10px]">
                      {{ agreement.signatureStatus }}
                    </span>
                  </div>
                  <div class="bg-slate-900/80 p-3 rounded-lg border border-slate-800 space-y-1 font-mono text-[11px]">
                    <div class="text-slate-400">Agreement ID: <span class="text-white">{{ agreement.agreementId }}</span></div>
                    <div class="text-slate-400">Discounted Value: <span class="text-emerald-400">\${{ agreement.annualContractValue | number:'1.2-2' }}</span> ({{ agreement.discountAppliedPercent }}% off)</div>
                    <div class="text-slate-400">Price Lock Guarantee: <span class="text-cyan-400">{{ agreement.priceLockYears }} Years</span></div>
                  </div>
                </div>
              }
            </div>
          } @else {
            <div class="text-center py-16 text-slate-400 text-xs">
              Select an account from the radar to inspect churn risk drivers and trigger retention playbooks.
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class ChurnRescueCockpitComponent {
  readonly accounts = signal<ChurnRiskAssessment[]>([
    {
      accountId: 'acc_globex_corp',
      accountName: 'Globex Logistics Systems',
      annualRecurringRevenue: 124000,
      churnPropensityScore: 84,
      riskLevel: 'SevereCritical',
      primaryRiskDrivers: ['Usage declined 48.5% in last 30d', 'Negative ticket sentiment trend detected', 'Primary executive champion changed roles'],
      daysUntilRenewal: 42,
      recommendedPlaybook: 'DiscountLock'
    },
    {
      accountId: 'acc_zenith_media',
      accountName: 'Zenith OmniMedia Group',
      annualRecurringRevenue: 215000,
      churnPropensityScore: 68,
      riskLevel: 'High',
      primaryRiskDrivers: ['Usage declined 35.0% in last 30d', 'User active session frequency severely reduced'],
      daysUntilRenewal: 28,
      recommendedPlaybook: 'DiscountLock'
    },
    {
      accountId: 'acc_novus_health',
      accountName: 'Novus Digital Health',
      annualRecurringRevenue: 86000,
      churnPropensityScore: 22,
      riskLevel: 'Low',
      primaryRiskDrivers: ['Stable recurring usage patterns'],
      daysUntilRenewal: 85,
      recommendedPlaybook: 'UsageCredits'
    }
  ]);

  readonly selectedAccount = signal<ChurnRiskAssessment | null>(this.accounts()[0]);
  readonly rescuedArr = signal(339000);

  readonly renewalAgreement = signal<RenewalAgreement | null>({
    agreementId: 'agr_escrow_globex',
    accountId: 'acc_globex_corp',
    annualContractValue: 105400,
    discountAppliedPercent: 15,
    priceLockYears: 2,
    signatureStatus: 'PendingSignature'
  });

  readonly totalArrAtRisk = computed(() =>
    this.accounts()
      .filter(a => a.churnPropensityScore >= 60)
      .reduce((acc, a) => acc + a.annualRecurringRevenue, 0)
  );

  readonly highRiskCount = computed(() =>
    this.accounts().filter(a => a.churnPropensityScore >= 60).length
  );

  selectAccount(acc: ChurnRiskAssessment): void {
    this.selectedAccount.set(acc);
  }

  executePlaybook(acc: ChurnRiskAssessment): void {
    // Lower churn propensity score as rescue playbook is executed
    this.accounts.update(list =>
      list.map(a =>
        a.accountId === acc.accountId
          ? { ...a, churnPropensityScore: Math.max(15, a.churnPropensityScore - 30), riskLevel: 'Moderate' }
          : a
      )
    );
    if (this.selectedAccount()?.accountId === acc.accountId) {
      this.selectedAccount.update(a =>
        a ? { ...a, churnPropensityScore: Math.max(15, a.churnPropensityScore - 30), riskLevel: 'Moderate' } : null
      );
    }
    this.rescuedArr.update(v => v + acc.annualRecurringRevenue);
  }

  generateRenewalAgreement(): void {
    const acc = this.selectedAccount();
    if (!acc) return;

    this.renewalAgreement.set({
      agreementId: `agr_escrow_${Date.now()}`,
      accountId: acc.accountId,
      annualContractValue: acc.annualRecurringRevenue * 0.85,
      discountAppliedPercent: 15,
      priceLockYears: 2,
      signatureStatus: 'PendingSignature'
    });
  }

  getRiskBadgeClass(risk: string): string {
    switch (risk) {
      case 'SevereCritical':
        return 'bg-rose-900/80 text-rose-300 border-rose-700';
      case 'High':
        return 'bg-amber-900/80 text-amber-300 border-amber-700';
      case 'Moderate':
        return 'bg-yellow-900/80 text-yellow-300 border-yellow-700';
      default:
        return 'bg-emerald-900/80 text-emerald-300 border-emerald-700';
    }
  }
}
