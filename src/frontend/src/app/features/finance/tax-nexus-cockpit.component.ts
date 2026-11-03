import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TaxNexus {
  jurisdictionCode: string;
  jurisdictionName: string;
  statutoryRevenueLimitUsd: number;
  statutoryTransactionLimit: number;
  currentTrailingRevenueUsd: number;
  currentTrailingTransactions: number;
  proximityPercentage: number;
  state: 'SafeBelowThreshold' | 'ApproachingNexusLimit' | 'NexusTriggeredRegistrationRequired' | 'RegisteredAndCollecting';
  estimatedTaxLiabilityUsd: number;
}

@Component({
  selector: 'app-tax-nexus-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="tax-nexus-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">Horizon 94</span>
            <span class="text-xs text-slate-400">Global Indirect Taxation & Economic Nexus</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Global Tax Nexus Tracking & Remittance Engine</h1>
          <p class="text-sm text-slate-400">Cross-border economic nexus proximity alarms, automated VAT/GST escrow calculation, and statutory filing readiness.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-jurisdictions">
          <div class="text-xs font-medium text-slate-400 uppercase">Monitored Jurisdictions</div>
          <div class="text-2xl font-bold text-white mt-1">{{ jurisdictions().length }} Global Zones</div>
          <div class="text-xs text-slate-400 mt-1">US States, EU OSS & UK HMRC</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-tax-reserve">
          <div class="text-xs font-medium text-slate-400 uppercase">Accrued Tax Liability Reserve</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">\${{ totalTaxLiability() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Escrow held for periodic remittance</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-nexus-breaches">
          <div class="text-xs font-medium text-slate-400 uppercase">Registration Actions Required</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ registrationRequiredCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Breached economic nexus threshold</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-collecting-zones">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Collecting Zones</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ activeCollectingCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Automated sales tax remittance</div>
        </div>
      </section>

      <!-- Jurisdictions Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Statutory Nexus Proximity Matrix</h2>
          <div class="space-y-3">
            @for (jur of jurisdictions(); track jur.jurisdictionCode) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedJurisdiction()?.jurisdictionCode === jur.jurisdictionCode"
                [class.ring-emerald-500]="selectedJurisdiction()?.jurisdictionCode === jur.jurisdictionCode"
                (click)="selectJurisdiction(jur)"
                data-testid="tax-jurisdiction-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="font-semibold text-white text-base">{{ jur.jurisdictionName }} ({{ jur.jurisdictionCode }})</h3>
                    <p class="text-xs text-slate-400 mt-0.5">Trailing Revenue: \${{ jur.currentTrailingRevenueUsd | number:'1.0-0' }} / \${{ jur.statutoryRevenueLimitUsd | number:'1.0-0' }}</p>
                  </div>
                  <div class="text-right">
                    <span class="text-xs px-2.5 py-1 rounded font-medium"
                      [class.bg-emerald-500/20]="jur.state === 'RegisteredAndCollecting' || jur.state === 'SafeBelowThreshold'"
                      [class.text-emerald-400]="jur.state === 'RegisteredAndCollecting' || jur.state === 'SafeBelowThreshold'"
                      [class.bg-amber-500/20]="jur.state === 'ApproachingNexusLimit'"
                      [class.text-amber-400]="jur.state === 'ApproachingNexusLimit'"
                      [class.bg-rose-500/20]="jur.state === 'NexusTriggeredRegistrationRequired'"
                      [class.text-rose-400]="jur.state === 'NexusTriggeredRegistrationRequired'"
                    >
                      {{ jur.state }}
                    </span>
                    <div class="text-xs text-slate-300 mt-1">Proximity: {{ jur.proximityPercentage }}%</div>
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-2 mt-3 pt-3 border-t border-slate-700/60 text-xs">
                  <div>
                    <span class="text-slate-400">Transactions:</span>
                    <span class="font-semibold text-white ml-1">{{ jur.currentTrailingTransactions }} / {{ jur.statutoryTransactionLimit }}</span>
                  </div>
                  <div>
                    <span class="text-slate-400">Estimated Liability:</span>
                    <span class="font-semibold text-amber-300 ml-1">\${{ jur.estimatedTaxLiabilityUsd | number:'1.0-0' }}</span>
                  </div>
                </div>

                <div class="mt-4 flex items-center justify-between pt-2">
                  <span class="text-xs text-slate-500">Statute: Wayfair Economic Nexus Rules</span>
                  @if (jur.state === 'NexusTriggeredRegistrationRequired') {
                    <button
                      (click)="registerAndCollect(jur.jurisdictionCode); $event.stopPropagation()"
                      data-testid="btn-register-nexus"
                      class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold rounded shadow transition-colors"
                    >
                      Complete Registration & Collect
                    </button>
                  } @else {
                    <span class="text-xs text-slate-400">Status Verified</span>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Fiscal Registration Detail</h2>
            @if (selectedJurisdiction(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Selected Jurisdiction</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.jurisdictionName }}</div>
                  <div class="text-xs text-emerald-400 mt-1">Statute Limit: \${{ sel.statutoryRevenueLimitUsd | number:'1.0-0' }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Threshold Proximity:</span>
                    <span class="font-mono text-amber-300">{{ sel.proximityPercentage }}%</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Tax Liability Reserve:</span>
                    <span class="font-mono text-emerald-400">\${{ sel.estimatedTaxLiabilityUsd | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Registration State:</span>
                    <span class="font-mono text-white">{{ sel.state }}</span>
                  </div>
                </div>

                @if (sel.state === 'NexusTriggeredRegistrationRequired') {
                  <button
                    (click)="registerAndCollect(sel.jurisdictionCode)"
                    class="w-full mt-4 py-2 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-semibold rounded text-sm shadow transition-all"
                  >
                    Authorize Automated Remittance
                  </button>
                }
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select a jurisdiction to view tax filing details.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-emerald-400 font-semibold">Automated Compliance:</span> Automatically calculates local destination rates and syncs tax lines into customer checkout.
          </div>
        </div>
      </section>
    </div>
  `
})
export class TaxNexusCockpitComponent {
  jurisdictions = signal<TaxNexus[]>([
    {
      jurisdictionCode: 'US-CA',
      jurisdictionName: 'California, USA',
      statutoryRevenueLimitUsd: 500000,
      statutoryTransactionLimit: 200,
      currentTrailingRevenueUsd: 480000,
      currentTrailingTransactions: 185,
      proximityPercentage: 96.0,
      state: 'ApproachingNexusLimit',
      estimatedTaxLiabilityUsd: 38400
    },
    {
      jurisdictionCode: 'US-NY',
      jurisdictionName: 'New York, USA',
      statutoryRevenueLimitUsd: 500000,
      statutoryTransactionLimit: 100,
      currentTrailingRevenueUsd: 520000,
      currentTrailingTransactions: 115,
      proximityPercentage: 104.0,
      state: 'NexusTriggeredRegistrationRequired',
      estimatedTaxLiabilityUsd: 44200
    },
    {
      jurisdictionCode: 'US-TX',
      jurisdictionName: 'Texas, USA',
      statutoryRevenueLimitUsd: 500000,
      statutoryTransactionLimit: 200,
      currentTrailingRevenueUsd: 310000,
      currentTrailingTransactions: 95,
      proximityPercentage: 62.0,
      state: 'SafeBelowThreshold',
      estimatedTaxLiabilityUsd: 0
    },
    {
      jurisdictionCode: 'EU-OSS',
      jurisdictionName: 'European Union One-Stop-Shop (OSS)',
      statutoryRevenueLimitUsd: 10000,
      statutoryTransactionLimit: 50,
      currentTrailingRevenueUsd: 65000,
      currentTrailingTransactions: 320,
      proximityPercentage: 650.0,
      state: 'RegisteredAndCollecting',
      estimatedTaxLiabilityUsd: 13650
    },
    {
      jurisdictionCode: 'UK-HMRC',
      jurisdictionName: 'United Kingdom (HMRC VAT)',
      statutoryRevenueLimitUsd: 85000,
      statutoryTransactionLimit: 100,
      currentTrailingRevenueUsd: 78000,
      currentTrailingTransactions: 88,
      proximityPercentage: 91.8,
      state: 'ApproachingNexusLimit',
      estimatedTaxLiabilityUsd: 15600
    }
  ]);

  selectedJurisdiction = signal<TaxNexus | null>(this.jurisdictions()[1]);

  totalTaxLiability = computed(() =>
    this.jurisdictions().reduce((sum, j) => sum + j.estimatedTaxLiabilityUsd, 0)
  );

  registrationRequiredCount = computed(() =>
    this.jurisdictions().filter(j => j.state === 'NexusTriggeredRegistrationRequired').length
  );

  activeCollectingCount = computed(() =>
    this.jurisdictions().filter(j => j.state === 'RegisteredAndCollecting').length
  );

  selectJurisdiction(jurisdiction: TaxNexus): void {
    this.selectedJurisdiction.set(jurisdiction);
  }

  registerAndCollect(jurisdictionCode: string): void {
    this.jurisdictions.update(list =>
      list.map(j => {
        if (j.jurisdictionCode === jurisdictionCode) {
          return {
            ...j,
            state: 'RegisteredAndCollecting'
          };
        }
        return j;
      })
    );

    if (this.selectedJurisdiction()?.jurisdictionCode === jurisdictionCode) {
      this.selectedJurisdiction.update(curr => {
        if (!curr) return null;
        return {
          ...curr,
          state: 'RegisteredAndCollecting'
        };
      });
    }
  }
}
