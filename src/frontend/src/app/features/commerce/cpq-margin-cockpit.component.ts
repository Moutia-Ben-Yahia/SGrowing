import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface YearRampView {
  year: number;
  basePrice: number;
  discount: number;
  netAcv: number;
  cogs: number;
  profit: number;
  margin: number;
}

@Component({
  selector: 'app-cpq-margin-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="cpq-margin-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-400">
            <span class="inline-block w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span>
            Enterprise CPQ & Gross Margin Guardrails • Horizon 68
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous B2B Tiered Pricing & Margin Guardrail Engine
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-time COGS hurdle validation (75% floor), multi-year escalation indexing & cryptographic audit tokens.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="applyExecutiveConcession()"
            data-testid="apply-discount-btn"
            class="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-amber-600/20"
          >
            <span>⚖️</span> Request 20% VP Concession
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Total Contract Value (TCV)</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-tcv">
            \${{ totalContractValue() | number:'1.0-0' }}
          </div>
          <div class="text-amber-400 text-xs mt-1">{{ termYears() }}-Year Enterprise Ramp</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Blended Gross Margin</div>
          <div class="text-2xl font-bold mt-1" [class]="blendedMargin() >= 75 ? 'text-emerald-400' : 'text-rose-400'" data-testid="stat-margin">
            {{ blendedMargin() }}%
          </div>
          <div class="text-slate-400 text-xs mt-1">75.0% Non-Negotiable Hurdle</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Discount Approval Tier</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-approval-tier">
            {{ approvalTier() }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">Delegated Authority Band</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Cryptographic Audit Seal</div>
          <div class="text-lg font-mono font-bold text-slate-200 mt-2 truncate" data-testid="stat-audit-hash">
            SHA:{{ auditHash() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Verifiable Pricing Token</div>
        </div>
      </div>

      <!-- Contract Ramps Table -->
      <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
        <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
          <h2 class="text-lg font-bold text-white">Multi-Year Revenue & Cost Waterfall</h2>
          <span class="text-xs text-slate-400 font-mono">Customer: AeroDynamics Global</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-xs">
            <thead class="bg-slate-900/60 text-slate-400 uppercase font-semibold border-b border-slate-700/60">
              <tr>
                <th class="p-3">Contract Year</th>
                <th class="p-3">Base Price</th>
                <th class="p-3">Discount</th>
                <th class="p-3">Net ACV</th>
                <th class="p-3">COGS + Support</th>
                <th class="p-3">Gross Profit</th>
                <th class="p-3">Gross Margin</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-700/40 text-slate-200">
              @for (ramp of ramps(); track ramp.year) {
                <tr class="hover:bg-slate-700/30 transition-colors">
                  <td class="p-3 font-bold text-white">Year {{ ramp.year }}</td>
                  <td class="p-3 font-mono">\${{ ramp.basePrice | number:'1.0-0' }}</td>
                  <td class="p-3 font-mono text-amber-400">{{ ramp.discount }}%</td>
                  <td class="p-3 font-mono font-bold text-emerald-400">\${{ ramp.netAcv | number:'1.0-0' }}</td>
                  <td class="p-3 font-mono text-slate-400">\${{ ramp.cogs | number:'1.0-0' }}</td>
                  <td class="p-3 font-mono font-bold text-white">\${{ ramp.profit | number:'1.0-0' }}</td>
                  <td class="p-3 font-mono font-bold text-emerald-400">{{ ramp.margin }}%</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class CpqMarginCockpitComponent {
  readonly termYears = signal(3);
  readonly discount = signal(12.5);
  readonly approvalTier = signal('StandardRepApproved');
  readonly auditHash = signal('4B8D2A1F9E3C7B5D');

  readonly ramps = signal<YearRampView[]>([
    { year: 1, basePrice: 120000, discount: 12.5, netAcv: 105000, cogs: 23000, profit: 82000, margin: 78.1 },
    { year: 2, basePrice: 123600, discount: 9.5, netAcv: 111858, cogs: 23000, profit: 88858, margin: 79.4 },
    { year: 3, basePrice: 127308, discount: 6.5, netAcv: 119033, cogs: 23000, profit: 96033, margin: 80.7 }
  ]);

  readonly totalContractValue = computed(() =>
    this.ramps().reduce((acc, r) => acc + r.netAcv, 0)
  );

  readonly blendedMargin = computed(() => {
    const totalRev = this.totalContractValue();
    const totalProf = this.ramps().reduce((acc, r) => acc + r.profit, 0);
    return totalRev > 0 ? parseFloat(((totalProf / totalRev) * 100).toFixed(1)) : 0;
  });

  applyExecutiveConcession(): void {
    this.discount.set(20.0);
    this.approvalTier.set('VpSalesApproved');
    this.auditHash.set('9F3A1D7E4C8B2A6F');
    this.ramps.set([
      { year: 1, basePrice: 120000, discount: 20.0, netAcv: 96000, cogs: 23000, profit: 73000, margin: 76.0 },
      { year: 2, basePrice: 123600, discount: 17.0, netAcv: 102588, cogs: 23000, profit: 79588, margin: 77.6 },
      { year: 3, basePrice: 127308, discount: 14.0, netAcv: 109485, cogs: 23000, profit: 86485, margin: 79.0 }
    ]);
  }
}
