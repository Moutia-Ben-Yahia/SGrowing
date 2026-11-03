import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CommissionRecord {
  commissionId: string;
  dealId: string;
  accountName: string;
  repId: string;
  repName: string;
  dealArr: number;
  quotaAttainmentPct: number;
  tier: 'Baseline' | 'Standard' | 'Accelerator' | 'SuperAccelerator';
  totalCommission: number;
  immediateDisbursedAmount: number;
  clawbackEscrowAmount: number;
  status: 'PendingSigning' | 'DisbursedImmediate' | 'HeldInClawbackEscrow' | 'FullySettled';
  closedAt: string;
  escrowReleaseDate: string;
  payoutHash: string;
}

@Component({
  selector: 'app-commission-settlement-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="commission-settlement-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-amber-500/20 text-amber-400 border border-amber-500/30">Horizon 101</span>
            <span class="text-xs text-slate-400">Sales Compensation & Real-Time Escrow</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Real-Time Sales Commission Settlement & Rep Escrow</h1>
          <p class="text-sm text-slate-400">Tiered quota accelerators (up to 20%), automated 80/20 milestone vesting, and 90-day clawback escrow holdbacks.</p>
        </div>
      </header>

      <!-- KPI Summary Cards -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-commissions">
          <div class="text-xs font-medium text-slate-400 uppercase">Commissions Disbursed</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ totalPaid() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Immediate upfront 80% payouts</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-escrow-balance">
          <div class="text-xs font-medium text-slate-400 uppercase">Clawback Escrow Reserve</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">\${{ totalInEscrow() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Held until customer invoice collection</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-avg-attainment">
          <div class="text-xs font-medium text-slate-400 uppercase">Average Quota Attainment</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ avgAttainment() | number:'1.1-1' }}%</div>
          <div class="text-xs text-slate-400 mt-1">Team attainment trajectory</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-settled-deals">
          <div class="text-xs font-medium text-slate-400 uppercase">Deals Tracked</div>
          <div class="text-2xl font-bold text-white mt-1">{{ records().length }} Deals</div>
          <div class="text-xs text-slate-400 mt-1">Cryptographically hashed settlements</div>
        </div>
      </section>

      <!-- Commissions Table -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Commission Settlement Ledger</h2>
          <span class="text-xs text-slate-400">Total: {{ records().length }} Records</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="commissions-table">
            <thead class="text-xs uppercase bg-slate-800 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Deal / Account</th>
                <th class="py-3 px-4">Sales Rep</th>
                <th class="py-3 px-4">ARR / Quota</th>
                <th class="py-3 px-4">Tier</th>
                <th class="py-3 px-4">Total Comm.</th>
                <th class="py-3 px-4">Escrow (20%)</th>
                <th class="py-3 px-4">Status</th>
                <th class="py-3 px-4 text-right">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-750 text-slate-300">
              @for (r of records(); track r.commissionId) {
                <tr class="hover:bg-slate-800/50 transition-colors" [attr.data-testid]="'commission-row-' + r.commissionId">
                  <td class="py-3 px-4">
                    <div class="font-medium text-white">{{ r.accountName }}</div>
                    <div class="text-xs text-slate-400 font-mono">{{ r.dealId }}</div>
                  </td>
                  <td class="py-3 px-4">
                    <div class="font-medium text-slate-200">{{ r.repName }}</div>
                    <div class="text-xs text-slate-400">{{ r.repId }}</div>
                  </td>
                  <td class="py-3 px-4">
                    <div class="font-semibold text-white">\${{ r.dealArr | number:'1.0-0' }}</div>
                    <div class="text-xs text-cyan-400">{{ r.quotaAttainmentPct | number:'1.1-1' }}% Attained</div>
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-semibold"
                          [ngClass]="{
                            'bg-purple-500/20 text-purple-300': r.tier === 'SuperAccelerator',
                            'bg-cyan-500/20 text-cyan-300': r.tier === 'Accelerator',
                            'bg-emerald-500/20 text-emerald-300': r.tier === 'Standard',
                            'bg-slate-700 text-slate-300': r.tier === 'Baseline'
                          }">
                      {{ r.tier }}
                    </span>
                  </td>
                  <td class="py-3 px-4 font-semibold text-white">
                    \${{ r.totalCommission | number:'1.0-0' }}
                  </td>
                  <td class="py-3 px-4 font-mono text-amber-400 text-xs">
                    \${{ r.clawbackEscrowAmount | number:'1.0-0' }}
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-medium"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-400': r.status === 'FullySettled',
                            'bg-amber-500/20 text-amber-400': r.status === 'HeldInClawbackEscrow',
                            'bg-cyan-500/20 text-cyan-400': r.status === 'DisbursedImmediate'
                          }">
                      {{ r.status }}
                    </span>
                  </td>
                  <td class="py-3 px-4 text-right">
                    @if (r.status === 'HeldInClawbackEscrow') {
                      <button class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
                              (click)="releaseEscrow(r.commissionId)"
                              [attr.data-testid]="'btn-release-' + r.commissionId">
                        Release Escrow
                      </button>
                    } @else {
                      <span class="text-xs text-slate-500 font-mono">Settled</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Action Toast -->
      @if (lastReleaseNotice()) {
        <div class="p-3 bg-emerald-950/60 border border-emerald-500/40 rounded-xl text-emerald-300 text-xs flex items-center justify-between"
             data-testid="release-notice">
          <span>{{ lastReleaseNotice() }}</span>
          <button (click)="lastReleaseNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class CommissionSettlementCockpitComponent {
  readonly records = signal<CommissionRecord[]>([
    {
      commissionId: 'COMM-2026-101',
      dealId: 'DEAL-ENT-901',
      accountName: 'Global FinCorp Americas',
      repId: 'REP-401',
      repName: 'Victoria Sterling',
      dealArr: 350000,
      quotaAttainmentPct: 128.5,
      tier: 'SuperAccelerator',
      totalCommission: 70000,
      immediateDisbursedAmount: 56000,
      clawbackEscrowAmount: 14000,
      status: 'HeldInClawbackEscrow',
      closedAt: '2026-10-20T00:00:00Z',
      escrowReleaseDate: '2027-01-18T00:00:00Z',
      payoutHash: 'COMM-HASH-901-A48F'
    },
    {
      commissionId: 'COMM-2026-102',
      dealId: 'DEAL-ENT-902',
      accountName: 'Hyperion Cloud Systems',
      repId: 'REP-402',
      repName: 'Marcus Vance',
      dealArr: 180000,
      quotaAttainmentPct: 104.2,
      tier: 'Accelerator',
      totalCommission: 27000,
      immediateDisbursedAmount: 21600,
      clawbackEscrowAmount: 5400,
      status: 'HeldInClawbackEscrow',
      closedAt: '2026-10-06T00:00:00Z',
      escrowReleaseDate: '2027-01-04T00:00:00Z',
      payoutHash: 'COMM-HASH-902-B12C'
    },
    {
      commissionId: 'COMM-2026-103',
      dealId: 'DEAL-ENT-903',
      accountName: 'Nordic Health Telematics',
      repId: 'REP-403',
      repName: 'Elena Rostova',
      dealArr: 95000,
      quotaAttainmentPct: 92.0,
      tier: 'Standard',
      totalCommission: 9500,
      immediateDisbursedAmount: 7600,
      clawbackEscrowAmount: 1900,
      status: 'FullySettled',
      closedAt: '2026-07-31T00:00:00Z',
      escrowReleaseDate: '2026-10-29T00:00:00Z',
      payoutHash: 'COMM-HASH-903-E88D'
    }
  ]);

  readonly lastReleaseNotice = signal<string | null>(null);

  readonly totalPaid = computed(() =>
    this.records().reduce((sum, r) => sum + r.immediateDisbursedAmount + (r.status === 'FullySettled' ? r.clawbackEscrowAmount : 0), 0)
  );

  readonly totalInEscrow = computed(() =>
    this.records().filter(r => r.status === 'HeldInClawbackEscrow').reduce((sum, r) => sum + r.clawbackEscrowAmount, 0)
  );

  readonly avgAttainment = computed(() => {
    const list = this.records();
    return list.length > 0 ? list.reduce((sum, r) => sum + r.quotaAttainmentPct, 0) / list.length : 100;
  });

  releaseEscrow(commissionId: string): void {
    const record = this.records().find(r => r.commissionId === commissionId);
    if (!record) return;

    this.records.update(list =>
      list.map(r => r.commissionId === commissionId ? {
        ...r,
        status: 'FullySettled'
      } : r)
    );

    this.lastReleaseNotice.set(`Disbursed \$${record.clawbackEscrowAmount.toLocaleString()} escrow to ${record.repName}. Commission is now Fully Settled.`);
  }
}
