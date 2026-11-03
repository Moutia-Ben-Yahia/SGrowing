import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RevenueContract {
  contractId: string;
  customerName: string;
  totalContractValue: number;
  deferredRevenue: number;
  recognizedRevenue: number;
  monthlyTrancheAmount: number;
  performanceObligationsCount: number;
  auditTrailHash: string;
  complianceStatus: 'ASC_606_Compliant' | 'PendingSchedule';
}

@Component({
  selector: 'app-asc606-revenue-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="asc606-revenue-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">Horizon 89</span>
            <span class="text-xs text-slate-400">ASC 606 & IFRS 15 Revenue Compliance</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Revenue Recognition & Deferred Ledger</h1>
          <p class="text-sm text-slate-400">Multi-element performance obligation scheduling, automated straight-line amortization, and GAAP audit-trail hashing.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-deferred-revenue">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Deferred Revenue</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">\${{ totalDeferredRevenue() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Unearned contract liability</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-recognized-revenue">
          <div class="text-xs font-medium text-slate-400 uppercase">Recognized ARR to Date</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ totalRecognizedRevenue() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Earned via performance obligations</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-contracts">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Enterprise Master Agreements</div>
          <div class="text-2xl font-bold text-white mt-1">{{ contracts().length }} Contracts</div>
          <div class="text-xs text-slate-400 mt-1">100% ASC 606 & IFRS 15 Compliant</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-monthly-amortization">
          <div class="text-xs font-medium text-slate-400 uppercase">Monthly Recognition Rate</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">\${{ monthlyRunRate() | number:'1.0-0' }}/mo</div>
          <div class="text-xs text-slate-400 mt-1">Straight-line monthly burn</div>
        </div>
      </section>

      <!-- Contracts Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Deferred Revenue Schedule & Contracts</h2>
          <div class="space-y-3">
            @for (c of contracts(); track c.contractId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedContract()?.contractId === c.contractId"
                [class.ring-emerald-500]="selectedContract()?.contractId === c.contractId"
                (click)="selectContract(c)"
                data-testid="revenue-contract-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="font-semibold text-white text-base">{{ c.customerName }}</h3>
                    <p class="text-xs text-slate-400 mt-0.5">TCV: \${{ c.totalContractValue | number:'1.0-0' }} &bull; {{ c.performanceObligationsCount }} Deliverables</p>
                  </div>
                  <div class="text-right">
                    <span class="text-xs px-2.5 py-1 rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium">
                      {{ c.complianceStatus }}
                    </span>
                    <div class="text-xs text-slate-400 mt-1">Amortize: \${{ c.monthlyTrancheAmount | number:'1.0-0' }}/mo</div>
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-2 mt-3 pt-3 border-t border-slate-700/60 text-xs">
                  <div>
                    <span class="text-slate-400">Deferred Balance:</span>
                    <span class="font-semibold text-amber-300 ml-1">\${{ c.deferredRevenue | number:'1.0-0' }}</span>
                  </div>
                  <div>
                    <span class="text-slate-400">Recognized to Date:</span>
                    <span class="font-semibold text-emerald-400 ml-1">\${{ c.recognizedRevenue | number:'1.0-0' }}</span>
                  </div>
                </div>

                <div class="mt-4 flex items-center justify-between pt-2">
                  <div class="text-xs text-slate-500 font-mono">Hash: {{ c.auditTrailHash }}</div>
                  <button
                    (click)="recognizeTranche(c.contractId); $event.stopPropagation()"
                    data-testid="btn-recognize-tranche"
                    class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold rounded shadow transition-colors"
                  >
                    Recognize Monthly Tranche
                  </button>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Audit Trail & Ledger Detail</h2>
            @if (selectedContract(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Selected Contract</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.customerName }}</div>
                  <div class="text-xs text-emerald-400 mt-1">Contract ID: {{ sel.contractId }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Total Contract Value:</span>
                    <span class="font-mono text-white">\${{ sel.totalContractValue | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Deferred Revenue:</span>
                    <span class="font-mono text-amber-300">\${{ sel.deferredRevenue | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Recognized Revenue:</span>
                    <span class="font-mono text-emerald-400">\${{ sel.recognizedRevenue | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Monthly Tranche:</span>
                    <span class="font-mono text-cyan-300">\${{ sel.monthlyTrancheAmount | number:'1.0-0' }}</span>
                  </div>
                </div>

                <div class="mt-4 pt-3 border-t border-slate-700/60" data-testid="amortization-schedule-table">
                  <div class="text-xs font-semibold text-slate-300 mb-2">GAAP Ledger Schedule:</div>
                  <div class="space-y-1 text-xs font-mono text-slate-400">
                    <div class="flex justify-between"><span>Period N:</span><span class="text-emerald-400">+\${{ sel.monthlyTrancheAmount | number:'1.0-0' }} Recognized</span></div>
                    <div class="flex justify-between"><span>Period N+1:</span><span>\${{ sel.monthlyTrancheAmount | number:'1.0-0' }} Scheduled</span></div>
                    <div class="flex justify-between"><span>Period N+2:</span><span>\${{ sel.monthlyTrancheAmount | number:'1.0-0' }} Scheduled</span></div>
                  </div>
                </div>
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select a contract to view schedule.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-emerald-400 font-semibold">SOX & GAAP Ready:</span> Cryptographic hash protects against retroactive revenue restatements.
          </div>
        </div>
      </section>
    </div>
  `
})
export class Asc606RevenueCockpitComponent {
  contracts = signal<RevenueContract[]>([
    {
      contractId: 'cntr_rev_01',
      customerName: 'Vertex Global Financial',
      totalContractValue: 240000,
      deferredRevenue: 180000,
      recognizedRevenue: 60000,
      monthlyTrancheAmount: 20000,
      performanceObligationsCount: 3,
      auditTrailHash: 'sha256-8a71b...09c4',
      complianceStatus: 'ASC_606_Compliant'
    },
    {
      contractId: 'cntr_rev_02',
      customerName: 'BioHealth Pharma Systems',
      totalContractValue: 120000,
      deferredRevenue: 90000,
      recognizedRevenue: 30000,
      monthlyTrancheAmount: 10000,
      performanceObligationsCount: 2,
      auditTrailHash: 'sha256-3f12e...64bb',
      complianceStatus: 'ASC_606_Compliant'
    },
    {
      contractId: 'cntr_rev_03',
      customerName: 'OmniCloud Aerospace',
      totalContractValue: 360000,
      deferredRevenue: 300000,
      recognizedRevenue: 60000,
      monthlyTrancheAmount: 30000,
      performanceObligationsCount: 4,
      auditTrailHash: 'sha256-99cd1...8e51',
      complianceStatus: 'ASC_606_Compliant'
    }
  ]);

  selectedContract = signal<RevenueContract | null>(this.contracts()[0]);

  totalDeferredRevenue = computed(() =>
    this.contracts().reduce((sum, c) => sum + c.deferredRevenue, 0)
  );

  totalRecognizedRevenue = computed(() =>
    this.contracts().reduce((sum, c) => sum + c.recognizedRevenue, 0)
  );

  monthlyRunRate = computed(() =>
    this.contracts().reduce((sum, c) => sum + c.monthlyTrancheAmount, 0)
  );

  selectContract(contract: RevenueContract): void {
    this.selectedContract.set(contract);
  }

  recognizeTranche(contractId: string): void {
    this.contracts.update(list =>
      list.map(c => {
        if (c.contractId === contractId && c.deferredRevenue >= c.monthlyTrancheAmount) {
          return {
            ...c,
            deferredRevenue: c.deferredRevenue - c.monthlyTrancheAmount,
            recognizedRevenue: c.recognizedRevenue + c.monthlyTrancheAmount
          };
        }
        return c;
      })
    );

    if (this.selectedContract()?.contractId === contractId) {
      this.selectedContract.update(curr => {
        if (curr && curr.deferredRevenue >= curr.monthlyTrancheAmount) {
          return {
            ...curr,
            deferredRevenue: curr.deferredRevenue - curr.monthlyTrancheAmount,
            recognizedRevenue: curr.recognizedRevenue + curr.monthlyTrancheAmount
          };
        }
        return curr;
      });
    }
  }
}
