import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PartnerDealView {
  dealId: string;
  partnerName: string;
  tier: string;
  prospectCompany: string;
  prospectDomain: string;
  arr: number;
  commission: number;
  status: 'ApprovedLocked' | 'PendingReview' | 'CollisionRejected';
  daysLeft: number;
}

@Component({
  selector: 'app-deal-registration-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="deal-registration-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-emerald-400">
            <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            Partner Ecosystem & Co-Selling • Horizon 64
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Partner Co-Selling & Deal Registration Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Cryptographic collision prevention locks, 90-day deal exclusivity & automated tiered commission escrow calculations.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="registerNewPartnerDeal()"
            data-testid="register-deal-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-emerald-600/20"
          >
            <span>🤝</span> Register Partner Deal
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Total Registered Partner ARR</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-total-arr">
            \${{ totalArr() | number:'1.0-0' }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">Co-Selling Ecosystem Pipeline</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Exclusivity Locked Deals</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-locked-count">
            {{ lockedCount() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Zero Channel Conflict Guaranteed</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Escrowed Partner Commissions</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-commissions">
            \${{ totalCommissions() | number:'1.0-0' }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">Tier-Weighted Payout Escrow</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Channel Conflict Collisions</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">0 Active</div>
          <div class="text-slate-400 text-xs mt-1">Deterministic Domain Hash Protection</div>
        </div>
      </div>

      <!-- Main Stage: Registered Deals & Exclusivity Console -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Deals Table (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Partner Registered Opportunities</h2>
            <span class="text-xs text-slate-400 font-mono">{{ deals().length }} Opportunities</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (deal of deals(); track deal.dealId) {
              <div
                (click)="selectDeal(deal)"
                [class.bg-slate-700/40]="selectedDeal()?.dealId === deal.dealId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'deal-row-' + deal.dealId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ deal.prospectCompany }}</span>
                    <span
                      [class]="deal.status === 'ApprovedLocked' ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-amber-900/60 text-amber-300 border-amber-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                      data-testid="deal-status-badge"
                    >
                      {{ deal.status }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Partner: <strong class="text-slate-200">{{ deal.partnerName }}</strong> ({{ deal.tier }}) • Domain: <span class="font-mono text-cyan-300">{{ deal.prospectDomain }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Estimated ARR: <strong class="text-white">\${{ deal.arr | number:'1.0-0' }}</strong> • Commission: <strong class="text-emerald-400">\${{ deal.commission | number:'1.0-0' }}</strong>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Lock Expiration</div>
                  <div class="text-sm font-bold text-emerald-400 font-mono">{{ deal.daysLeft }} days left</div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Approval & Co-Selling Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Co-Selling Exclusivity Guard</h3>
              <p class="text-xs text-slate-400">90-Day Protected Window</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-emerald-950 text-emerald-300 border border-emerald-800">
              Active Lock
            </span>
          </div>

          @if (selectedDeal(); as active) {
            <div class="space-y-4 text-xs">
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2">
                <div class="text-slate-400 font-bold">Deal Exclusivity Status:</div>
                <div class="flex items-center justify-between">
                  <span class="text-white font-bold text-sm">{{ active.prospectCompany }}</span>
                  <span class="text-emerald-400 font-mono font-bold">{{ active.daysLeft }} Days Protected</span>
                </div>
                <p class="text-slate-400 text-[11px] leading-relaxed">
                  First-to-register priority locks the domain against direct SDR prospecting and duplicate partner submissions.
                </p>
              </div>

              @if (active.status === 'PendingReview') {
                <button
                  (click)="approveDeal(active)"
                  data-testid="approve-deal-btn"
                  class="w-full py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold rounded-lg text-xs shadow transition-colors"
                >
                  Approve 90-Day Exclusivity Lock
                </button>
              } @else {
                <div class="p-3 bg-slate-900 rounded-xl border border-emerald-900/60 text-emerald-300 text-center font-semibold text-xs">
                  ✓ Exclusivity Active: Partner commission locked in escrow.
                </div>
              }
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class DealRegistrationCockpitComponent {
  readonly deals = signal<PartnerDealView[]>([
    {
      dealId: 'deal_partner_701',
      partnerName: 'Accenture Cloud First',
      tier: 'Premier Global Integrator',
      prospectCompany: 'Global Telecommunications AG',
      prospectDomain: 'globaltelecom.de',
      arr: 350000,
      commission: 87500,
      status: 'ApprovedLocked',
      daysLeft: 75
    },
    {
      dealId: 'deal_partner_702',
      partnerName: 'ScaleForward Agency',
      tier: 'Certified Solution Provider',
      prospectCompany: 'Nordic Logistics AB',
      prospectDomain: 'nordiclogistics.se',
      arr: 120000,
      commission: 18000,
      status: 'PendingReview',
      daysLeft: 90
    }
  ]);

  readonly selectedDeal = signal<PartnerDealView | null>(this.deals()[1]);

  readonly totalArr = computed(() =>
    this.deals().reduce((acc, d) => acc + d.arr, 0)
  );

  readonly lockedCount = computed(() =>
    this.deals().filter(d => d.status === 'ApprovedLocked').length
  );

  readonly totalCommissions = computed(() =>
    this.deals().reduce((acc, d) => acc + d.commission, 0)
  );

  selectDeal(deal: PartnerDealView): void {
    this.selectedDeal.set(deal);
  }

  registerNewPartnerDeal(): void {
    const newDeal: PartnerDealView = {
      dealId: `deal_${Date.now()}`,
      partnerName: 'CloudScale Advisors',
      tier: 'Certified Solution Provider',
      prospectCompany: 'Helios Energy AG',
      prospectDomain: 'helios-energy.ch',
      arr: 200000,
      commission: 30000,
      status: 'ApprovedLocked',
      daysLeft: 90
    };

    this.deals.update(list => [newDeal, ...list]);
    this.selectedDeal.set(newDeal);
  }

  approveDeal(deal: PartnerDealView): void {
    this.deals.update(list =>
      list.map(d => d.dealId === deal.dealId ? { ...d, status: 'ApprovedLocked' } : d)
    );
    if (this.selectedDeal()?.dealId === deal.dealId) {
      this.selectedDeal.update(d => d ? { ...d, status: 'ApprovedLocked' } : null);
    }
  }
}
